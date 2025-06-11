namespace Validators
{
    internal class VatinValidator
    {
        public static bool Valid(string value)
        {
            if (!long.TryParse(value, out var digit)) return false;
            var digits = new int[value.Length];
            for (var i = 0; i < digits.Length; i++)
                digits[i] = (int)(digit / (long)Math.Pow(10, i) % 10L);
            return 
                value.Length == 10 && IsCompanyVatin(digits) ||
                value.Length == 12 && IsPersonVatin(digits);
        }

        private static bool IsCompanyVatin(int[] vatin)
        {
            Span<int> multiple = [ 8, 6, 4, 9, 5, 3, 10, 4, 2 ];
            var origin = vatin.AsSpan(1..);
            var check = 0;
            for (var i = 0; i < origin.Length; i++)
                check += multiple[i] * origin[i];
            check %= 11;
            check %= 10;
            return vatin[0] == check;
        }

        private static bool IsPersonVatin(int[] vatin)
        {
            Span<int> multiple11 = [8, 6, 4, 9, 5, 3, 10, 4, 2, 7];
            Span<int> multiple12 = [6, 4, 9, 5, 3, 10, 4, 2, 7, 3];
            var origin = vatin.AsSpan(2..);
            int check11 = 0, check12 = 0;
            for(var i = 0; i < origin.Length; i++)
            {
                check11 += origin[i] * multiple11[i];
                check12 += origin[i] * multiple12[i];
            }
            check11 %= 11;
            check11 %= 10;
            check12 += check11 * 8;
            check12 %= 11;
            check12 %= 10;
            return check11 == vatin[1] && check12 == vatin[0];
        }
    }
}
