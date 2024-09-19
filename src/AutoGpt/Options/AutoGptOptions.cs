namespace AutoGpt.Options;

public class AutoGptOptions
{
    /// <summary>
    /// 端点
    /// </summary>
    /// <returns></returns>
    public string Endpoint { get; set; }

    /// <summary>
    /// ApiKey
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// 对话模型
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// 推理次数
    /// </summary>
    public ushort NumOutputs { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new ArgumentNullException(nameof(Endpoint));
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new ArgumentNullException(nameof(ApiKey));
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new ArgumentNullException(nameof(Model));
        }
        
        NumOutputs = NumOutputs == 0 ? (ushort)1 : NumOutputs;
        
        if (NumOutputs > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(NumOutputs), "NumOutputs must be less than or equal to 8");
        }
    }
}