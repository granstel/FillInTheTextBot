using System;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    [Produces("application/json")]
    public class SberController : MessengerController<Request, Response>
    {
        public SberController(ISberService sberService, SberConfiguration configuration) : base(sberService, configuration)
        {
        }

        public override async Task<IActionResult> WebHook([FromBody] Request input, string token)
        {
            if (!ModelState.IsValid)
            {
                Log.Error(ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).JoinToString(Environment.NewLine));

                return Json(null);
            }

            var response = await base.WebHook(input, token);

            return Json(response);
        }
    }
}
