import copy
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path


BASE = os.environ.get("BASE_URL", "http://gateway:8080")
DF = os.environ.get("DIALOGFLOW_URL", "http://fitb-dialogflow")
UNAVAILABLE = os.environ.get("UNAVAILABLE_URL", "http://fitb-unavailable")
PROVIDER = "CONVERSATIONPROVIDER"
STATE = "CONVERSATIONSTATE"


def require(condition, label):
    if not condition:
        raise AssertionError(label)


def get(url):
    with urllib.request.urlopen(url, timeout=5) as response:
        return response.read().decode("utf-8")


def wait_ready(url):
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        try:
            get(url + "/health")
            return
        except (urllib.error.URLError, TimeoutError):
            time.sleep(1)
    raise AssertionError("HTTP readiness timeout")


def post(url, payload):
    request = urllib.request.Request(
        url + "/yandex",
        data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=5) as response:
        output = json.load(response)
    answer = output.get("response", {})
    require(bool(answer.get("text")), "non-empty Alice response")
    require(len(answer["text"]) <= 1024, "Alice text limit")
    require(len(answer.get("tts") or "") <= 1024, "Alice TTS limit")
    state_bytes = json.dumps(output.get("session_state", {}), ensure_ascii=False, separators=(",", ":")).encode()
    require(len(state_bytes) <= 1024, "Alice session_state limit")
    return output


class Session:
    def __init__(self, url=BASE, legacy=False):
        self.url = url
        self.session_id = "synthetic-" + uuid.uuid4().hex
        self.message_id = 0
        self.state = {"session": {}, "user": {}, "application": {}}
        if legacy:
            self.state["user"] = {"NEXTTEXTINDEX": 7, "ROTATIONSTATE": "synthetic-legacy-state"}
        self.last_request = None

    def payload(self, text, new=False):
        return {
            "meta": {"locale": "ru-RU", "timezone": "UTC", "client_id": "local-synthetic", "interfaces": {"screen": {}}},
            "session": {
                "session_id": self.session_id, "message_id": self.message_id,
                "skill_id": "local-synthetic", "user_id": "local-synthetic", "new": new,
            },
            "request": {"command": text, "original_utterance": text, "type": "SimpleUtterance",
                        "markup": {"dangerous_context": False}, "nlu": {"tokens": [], "entities": []}},
            "state": copy.deepcopy(self.state),
            "version": "1.0",
        }

    def accept(self, output):
        self.state["session"] = output.get("session_state", {})
        self.state["user"].update(output.get("user_state_update") or {})
        self.state["application"].update(output.get("application_state") or {})
        return output

    def say(self, text="", new=False):
        self.last_request = self.payload(text, new)
        self.message_id += 1
        return self.accept(post(self.url, self.last_request))


def includes(output, fragment, label):
    require(fragment.casefold() in output["response"]["text"].casefold(), label)


def story_state(output):
    session = output.get("session_state", {})
    require(session.get(PROVIDER) == "StoryDialog", "StoryDialog provider pin")
    require(isinstance(session.get(STATE), str), "opaque StoryDialog state")
    return json.loads(session[STATE])


def df_calls(url):
    lines = get(url + "/metrics").splitlines()
    return sum(float(line.split("}")[-1].strip().split()[0]) for line in lines
               if not line.startswith("#") and 'metric_name="dialogflow_DetectIntent_scope"' in line)


def check_contract():
    owner = Path("/contracts/owner.proto").read_bytes()
    export = Path("/contracts/export.proto").read_bytes()
    require(hashlib.sha256(owner).digest() == hashlib.sha256(export).digest(), "public contract export matches owner")


def ordinary_story():
    session = Session(legacy=True)
    first = session.say(new=True)
    includes(first, "Пикник на Луне", "story title")
    includes(first, "прилагательное", "first slot")
    old_state = copy.deepcopy(session.state)
    second = session.say("синтетический")
    includes(second, "предмет", "second slot")
    repeated = post(BASE, session.last_request)
    require(repeated["response"] == second["response"], "same HTTP message replays same answer")
    require(repeated["session_state"] == second["session_state"], "replay does not advance state")

    conflict = copy.deepcopy(session.last_request)
    conflict["request"].update(command="другой", original_utterance="другой")
    rejected = post(BASE, conflict)
    includes(rejected, "другим содержимым", "same message with different body rejected")

    stale = session.payload("потерянный ответ")
    stale["state"] = old_state
    session.message_id += 1
    current = session.accept(post(BASE, stale))
    includes(current, "История уже продвинулась", "stale version resynchronized")
    require(story_state(current)["v"] == story_state(second)["v"], "stale answer did not fill another slot")
    includes(current, "предмет", "resync returns current slot")
    pinned = copy.deepcopy(session.state["session"])
    ping = session.say("ping")
    require(ping["response"]["text"] == "pong", "ping")
    require(ping["session_state"] == pinned, "ping preserves provider state")

    includes(session.say("термос"), "бутербродов", "integer slot")
    invalid = session.say("нечисло")
    includes(invalid, "число", "invalid integer asks again")
    finished = session.say("3")
    includes(finished, "синтетический экипаж", "composed adjective")
    includes(finished, "термос", "composed object")
    includes(finished, "ровно 3", "composed integer")
    require(not finished["response"].get("end_session"), "completed story keeps skill open")
    require(session.state["user"]["NEXTTEXTINDEX"] == 7, "legacy index preserved")
    require(session.state["user"]["ROTATIONSTATE"] == "synthetic-legacy-state", "legacy rotation preserved")
    require("синтетический" not in session.state["session"][STATE], "answers absent from session_state")
    require(session.say("/repeat")["response"]["text"] == finished["response"]["text"], "repeat completed result")
    includes(session.say("другая история"), "Робот-курьер", "deterministic next story")
    return session


def multipart_commands():
    session = Session()
    first = session.say("/story robot-courier", new=True)
    includes(first, "имя", "explicit story selection")
    includes(session.say("/help"), "Команды", "help")
    includes(session.say("/repeat"), "имя", "repeat question")
    includes(session.say("стоп"), "посылке", "stop is a slot value")
    part = session.say("зонтик")
    includes(part, "Робот стоп", "first part result")
    includes(part, "Продолжим", "multipart waits for confirmation")
    require(session.say("/repeat")["response"]["text"] == part["response"]["text"], "repeat first part")
    includes(session.say("да"), "километров", "contextual yes continues")
    includes(session.say("2,5"), "наречие", "decimal slot")
    final = session.say("быстро")
    includes(final, "зонтик", "carry from first part")
    includes(final, "быстро", "second part completed")
    includes(session.say("/restart"), "имя", "restart resets slots")
    includes(session.say("/cancel"), "отмен", "cancel")
    includes(session.say("/restart"), "имя", "restart after cancellation")
    session.say("Бип")
    session.say("мяч")
    includes(session.say("/continue"), "километров", "explicit continue")
    print("PASS multipart, commands, carry")


def unavailable_and_switch(active):
    old_state = copy.deepcopy(active.state["session"])
    failed = post(UNAVAILABLE, active.payload("тест недоступности"))
    require(failed["session_state"] == old_state, "outage preserves active state")
    includes(failed, "Сервис историй", "outage has clear answer")
    fresh = Session(UNAVAILABLE).say(new=True)
    require(story_state(fresh)["v"] == 0, "outage retains initial conversation identity")

    df_before = df_calls(DF)
    blocked = post(DF, active.payload("проверка переключения"))
    require(blocked["session_state"] == old_state, "StoryDialog to Dialogflow keeps pin")
    includes(blocked, "новую сессию", "StoryDialog to Dialogflow requires new session")
    time.sleep(1)
    require(df_calls(DF) == df_before, "provider mismatch does not call Dialogflow")
    dialogflow = Session(DF)
    hello = dialogflow.say(new=True)
    includes(hello, "LOCAL_DIALOGFLOW_EMULATOR", "Dialogflow explicitly uses local emulator")
    require(hello["session_state"][PROVIDER] == "Dialogflow", "Dialogflow provider pin")
    blocked = post(BASE, dialogflow.payload("проверка переключения"))
    require(blocked["session_state"][PROVIDER] == "Dialogflow", "Dialogflow to StoryDialog keeps pin")
    includes(blocked, "новую сессию", "Dialogflow to StoryDialog requires new session")
    new_story = dialogflow.payload("", new=True)
    require(post(BASE, new_story)["session_state"][PROVIDER] == "StoryDialog", "new session can use StoryDialog")
    new_df = active.payload("", new=True)
    require(post(DF, new_df)["session_state"][PROVIDER] == "Dialogflow", "new session can use Dialogflow")
    time.sleep(1)
    require(df_calls(DF) > df_before, "Dialogflow metric detects positive control")
    print("PASS unavailable backend, provider switch and session pin")


def run():
    check_contract()
    mode = sys.argv[1] if len(sys.argv) > 1 else "smoke"
    wait_ready(BASE)
    if mode == "chat":
        session = Session()
        print(session.say(new=True)["response"]["text"])
        print("Введите /quit для выхода. Только синтетические ответы.")
        while True:
            try:
                text = input("> ")
            except EOFError:
                return
            if text == "/quit":
                return
            print(session.say(text)["response"]["text"])
    elif mode == "emulator":
        hello = Session().say(new=True)
        includes(hello, "LOCAL_DIALOGFLOW_EMULATOR", "Dialogflow emulator selected")
        require(hello["session_state"][PROVIDER] == "Dialogflow", "Dialogflow provider pin")
        print("PASS Dialogflow emulator")
    else:
        require(mode == "smoke", "supported modes: smoke, chat, emulator")
        require(os.environ.get("CONVERSATION_PROVIDER", "StoryDialog") == "StoryDialog", "full smoke requires StoryDialog")
        wait_ready(DF)
        wait_ready(UNAVAILABLE)
        calls = (df_calls(BASE), df_calls(UNAVAILABLE))
        active = ordinary_story()
        print("PASS ordinary story, exact HTTP replay, conflicting replay, resync, legacy state")
        multipart_commands()
        unavailable_and_switch(active)
        time.sleep(1)
        require((df_calls(BASE), df_calls(UNAVAILABLE)) == calls, "no Dialogflow fallback calls")
        print("PASS contract export, Alice limits and no Dialogflow fallback")


if __name__ == "__main__":
    try:
        run()
    except (AssertionError, urllib.error.URLError, TimeoutError, KeyError, ValueError) as error:
        # Do not print payloads, responses, session identifiers or exception bodies.
        detail = str(error) if isinstance(error, AssertionError) else type(error).__name__
        print("FAIL " + detail, file=sys.stderr)
        sys.exit(1)
