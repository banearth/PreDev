public class FReplicationGraphDebugInfo
{
    // 调试标记
    [System.Flags]
    public enum EFlags
    {
        ShowActors = 1 << 0,
        ShowClasses = 1 << 1,
        ShowNativeClasses = 1 << 2,
        ShowTotalCount = 1 << 3
    }

    // 输出设备
    private System.IO.TextWriter OutputDevice;
    
    // 调试标记
    public EFlags Flags { get; set; }
    
    // 是否显示空节点
    public bool bShowEmptyNodes { get; set; }
    
    // 缩进相关
    private string CurrentIndentString = "";
    private const string IndentString = "  ";

    public FReplicationGraphDebugInfo(System.IO.TextWriter outputDevice)
    {
        OutputDevice = outputDevice;
        bShowEmptyNodes = false;
    }

    // 输出日志
    public void Log(string message)
    {
        OutputDevice.WriteLine($"{CurrentIndentString}{message}");
    }

    // 增加缩进
    public void PushIndent()
    {
        CurrentIndentString += IndentString;
    }

    // 减少缩进
    public void PopIndent()
    {
        if (CurrentIndentString.Length >= IndentString.Length)
        {
            CurrentIndentString = CurrentIndentString.Substring(0, CurrentIndentString.Length - IndentString.Length);
        }
    }
}