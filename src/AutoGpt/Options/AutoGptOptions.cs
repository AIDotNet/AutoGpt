namespace AutoGpt.Options;

public class AutoGptOptions
{
    /// <summary>
    /// 端点
    /// </summary>
    /// <returns></returns>
    public string Endpoint { get; set; }

    /// <summary>
    /// 推理次数
    /// </summary>
    public ushort NumOutputs { get; set; }

    /// <summary>
    /// 温度
    /// </summary>
    public double? Temperature { get; set; } = 0.2;

    /// <summary>
    /// 核采样 限制考虑的词汇概率总和不超过top_p的值
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// 对于消息希望模型返回的选择数量 特定场景
    /// </summary>
    public int? NumChoicesPerMessage { get; set; }

    /// <summary>
    /// 停止序列 生成文本时如果遇到这些序列之一，则停止生成 可用来定义结束条件
    /// </summary>
    public string[] MultipleStopSequences { get; set; }

    /// <summary>
    /// 标记使用频率的惩罚尺度。通常应该在0到1之间，允许使用负数来鼓励标记的重用。默认值为0
    /// </summary>
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// 用于增加文档中新单词出现的概率。正值鼓励模型使用之前没有出现在文本中的新单词 默认值为0
    /// </summary>
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// 允许调整某些词汇的得分，从而影响它们被选中的概率
    /// </summary>
    public KeyValuePair<int, int> LogitBias { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new ArgumentNullException(nameof(Endpoint));
        }
        
        NumOutputs = NumOutputs == 0 ? (ushort)1 : NumOutputs;
        
        if (NumOutputs > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(NumOutputs), "NumOutputs must be less than or equal to 8");
        }
    }
}