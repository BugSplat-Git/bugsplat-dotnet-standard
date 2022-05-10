namespace Tests
{
    public static class StackTraceFactory
    {
        public static string CreateStackTrace()
        {
            return @"Exception: BugSplat rocks!
                Main.ThrowException () (at Assets/Main.cs:75)
                Main.SampleStackFrame2 () (at Assets/Main.cs:95)
                Main.SampleStackFrame1 () (at Assets/Main.cs:90)
                Main.SampleStackFrame0 () (at Assets/Main.cs:85)
                Main.GenerateSampleStackFramesAndThrow () (at Assets/Main.cs:80)
                Main.Update() (at Assets/Main.cs:69)";
        }
    }
}