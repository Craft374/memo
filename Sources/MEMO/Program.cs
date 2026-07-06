namespace Memo;

static class Program
{
    private static Mutex? instanceMutex;

    internal static readonly int ActivateMessage =
        NativeMethods.RegisterWindowMessage("MEMO_Craft374_Activate");

    [STAThread]
    static void Main()
    {
        instanceMutex = new Mutex(true, @"Local\MEMO_Craft374_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // 이미 실행 중이면 기존 창을 앞으로 가져오고 종료
            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, ActivateMessage, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        GC.KeepAlive(instanceMutex);
    }
}
