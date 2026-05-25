using System;

static class ExceptionHelpers
{
    public static int LineNumber(this Exception ex)
    {
        if (ex is null)
            return -1;

        int n;
        int i = ex.StackTrace.LastIndexOf(" ");
        if (i > -1)
        {
            string s = ex.StackTrace.Substring(i + 1);
            if (int.TryParse(s, out n))
                return n;
        }
        return -1;
    }
}