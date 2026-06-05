using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 集中管理 JSON 序列化设置，确保持久化文件格式一致。
/// </summary>
internal static class JsonOptionsFactory
{
    /// <summary>
    /// 创建小驼峰且带缩进的 JSON 序列化选项。
    /// </summary>
    public static JsonSerializerOptions Create(bool includeEnumConverter = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // hook 写入的状态文件使用 red、green 这类字符串枚举值。
        if (includeEnumConverter)
        {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        return options;
    }
}
