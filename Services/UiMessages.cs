using System;

namespace SnapAfghanistan.Native.Services
{
    public static class UiMessages
    {
        public static string Friendly(Exception exception)
        {
            var message = exception.Message ?? "عملیات کامل نشد.";
            if (message.IndexOf("members.tazkira_no", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("UNIQUE constraint failed: members.tazkira_no", StringComparison.OrdinalIgnoreCase) >= 0)
                return "این شماره تذکره قبلاً برای عضو دیگری ثبت شده است.";
            if (message.IndexOf("sectors.name", StringComparison.OrdinalIgnoreCase) >= 0)
                return "سکتوری با این نام قبلاً ثبت شده است.";
            if (message.IndexOf("subscription_payments.receipt_no", StringComparison.OrdinalIgnoreCase) >= 0)
                return "این شماره رسید قبلاً ثبت شده است.";
            if (message.IndexOf("database is locked", StringComparison.OrdinalIgnoreCase) >= 0)
                return "دیتابیس موقتاً مصروف است؛ چند ثانیه بعد دوباره امتحان کنید.";
            return message;
        }
    }
}
