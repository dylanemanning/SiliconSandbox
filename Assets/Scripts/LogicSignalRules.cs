using System.Collections.Generic;

public static class LogicSignalRules
{
    public static int ResolveSources(IEnumerable<int> sourceStates)
    {
        foreach (int state in sourceStates)
        {
            if (state == 1) return 1;
        }

        return 0;
    }
}