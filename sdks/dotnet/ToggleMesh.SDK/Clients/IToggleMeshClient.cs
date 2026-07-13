using ToggleMesh.Common.Contexts;
using ToggleMesh.SDK.Models;

namespace ToggleMesh.SDK.Clients;

public interface IToggleMeshClient
{
    bool IsEnabled(string flagKey, string? identity = null, bool defaultValue = false);
    bool IsEnabled<TContext>(string flagKey, TContext contextAttributes, bool defaultValue = false);
    bool IsEnabled<TContext>(string flagKey, string? identity, TContext contextAttributes, bool defaultValue = false);
    bool IsEnabled<TContext>(string flagKey, ref ToggleMeshUser<TContext> user, bool defaultValue = false) where TContext : IContextAccessor;

    string GetStringVariation(string flagKey, string? identity = null, string defaultValue = null!);
    string GetStringVariation<TContext>(string flagKey, TContext contextAttributes, string defaultValue = null!);
    string GetStringVariation<TContext>(string flagKey, string? identity, TContext contextAttributes, string defaultValue = null!);
    string GetStringVariation<TContext>(string flagKey, ref ToggleMeshUser<TContext> user, string defaultValue = null!) where TContext : IContextAccessor;

    T GetJsonVariation<T>(string flagKey, string? identity = null, T defaultValue = default!);
    T GetJsonVariation<TContext, T>(string flagKey, TContext contextAttributes, T defaultValue = default!);
    T GetJsonVariation<TContext, T>(string flagKey, string? identity, TContext contextAttributes, T defaultValue = default!);
    T GetJsonVariation<TContext, T>(string flagKey, ref ToggleMeshUser<TContext> user, T defaultValue = default!) where TContext : IContextAccessor;

    Guid? Evaluate(string flagKey, string? identity = null, Guid? defaultValue = null);
    Guid? Evaluate<TContext>(string flagKey, TContext contextAttributes, Guid? defaultValue = null);
    Guid? Evaluate<TContext>(string flagKey, string? identity, TContext contextAttributes, Guid? defaultValue = null);
    Guid? Evaluate<TContext>(string flagKey, ref ToggleMeshUser<TContext> user, Guid? defaultValue = null) where TContext : IContextAccessor;

    void Track(string eventName, string? identity = null, double? value = null);
    void Track<TProperties>(string eventName, TProperties properties, double? value = null);
    void Track<TProperties>(string eventName, string? identity, TProperties properties, double? value = null);
    void Track<TContext>(string eventName, ref ToggleMeshUser<TContext> user, double? value = null) where TContext : IContextAccessor;
    void Track<TContext, TProperties>(string eventName, ref ToggleMeshUser<TContext> user, ref TProperties properties, double? value = null) 
        where TContext : IContextAccessor 
        where TProperties : IContextAccessor;
}