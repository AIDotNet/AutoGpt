using System.ComponentModel;

using Microsoft.SemanticKernel;

namespace AutoGpt.Function;

public class DateTimeFunction
{
    /// <summary>
    /// 获取当前时间
    /// </summary>
    /// <returns></returns>
    [KernelFunction, Description("获取当前时间")]
    public static string GetNow()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 获取当前时间
    /// </summary>
    /// <param name="format">格式化字符串</param>
    /// <returns></returns>
    [KernelFunction, Description("获取指定格式的当前时间")]
    public static string GetNow(string format)
    {
        return DateTime.Now.ToString(format);
    }
}