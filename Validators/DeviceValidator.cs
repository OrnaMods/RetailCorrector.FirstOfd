namespace Validators
{
    internal class DeviceValidator
    {
        public static bool Valid(string value) =>
            long.TryParse(value, out var digit) && digit < 10e16;
    }
}
