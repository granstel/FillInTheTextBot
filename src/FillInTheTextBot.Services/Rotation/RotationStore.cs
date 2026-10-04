using System;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Последняя валидная версия ротации. Обновляется фоновой службой, читается при обработке запросов.
    /// </summary>
    public sealed class RotationStore
    {
        private volatile RotationSnapshot _snapshot;
        private volatile RotationCheck _lastCheck;

        /// <summary>null, пока ни одна версия не загружена.</summary>
        public RotationSnapshot Snapshot => _snapshot;

        public RotationCheck LastCheck => _lastCheck;

        public void Apply(RotationSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public void ReportCheck(DateTimeOffset checkedAt, string error)
        {
            _lastCheck = new RotationCheck(checkedAt, error);
        }
    }
}
