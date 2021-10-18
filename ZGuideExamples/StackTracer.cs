using System.Runtime.InteropServices;

namespace Examples
{
    public static class StackTracer
    {
        private const string LibName = "StackTracer";

        [DllImport(LibName)]
        public static extern void stack_tracer_init();

        [DllImport(LibName)]
        public static extern void stack_tracer_cleanup();
    }
}
