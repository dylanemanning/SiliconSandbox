using System.Collections.Generic;

public static class LogicSignalRules
{
    public static int ResolveSources(IEnumerable<int> sourceStates)
    {
        bool sawHigh = false;
        bool sawLow = false;

        foreach (int state in sourceStates)
        {
            if (state == 1)
            {
                sawHigh = true;
            }
            else if (state == 0)
            {
                sawLow = true;
            }
            else
            {
                return -1;
            }

            if (sawHigh && sawLow) return -1;
        }

        return sawHigh ? 1 : 0;
    }
}