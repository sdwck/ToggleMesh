namespace ToggleMesh.Common.Utils;

public static class StringCache
{
    private static readonly string[] SmallInts = new string[1000];

    static StringCache()
    {
        for (var i = 0; i < SmallInts.Length; i++)
        {
            SmallInts[i] = i.ToString();
        }
    }

    public static string GetIntString(int value)
    {
        if (value >= 0 && value < SmallInts.Length)
            return SmallInts[value];
        return value.ToString();
    }
}
