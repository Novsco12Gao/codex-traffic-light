namespace CodexTrafficLight.Core.Models;

public sealed record SessionRetentionOptions
{
    public TimeSpan GreenRetention { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan YellowRetention { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan RedRetention { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan LiveCliWorkRetention { get; init; } = TimeSpan.FromHours(6);
    public TimeSpan LiveVsCodePluginWorkRetention { get; init; } = TimeSpan.FromHours(2);
}
