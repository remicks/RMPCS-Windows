namespace RMPCS.Core;
public static class RestoreVerification
{
    // Windows can return success without creating a point (e.g. frequency limiting).
    public static bool IsNew(uint status, long previousMaximum, long matchingSequence) => status == 0 && previousMaximum >= -1 && matchingSequence >= 0 && matchingSequence > previousMaximum;
}
