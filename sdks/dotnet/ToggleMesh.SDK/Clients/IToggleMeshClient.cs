using ToggleMesh.SDK.Models;

namespace ToggleMesh.SDK.Clients;

public interface IToggleMeshClient
{
    bool IsEnabled(string flagKey, bool defaultValue = false);
    bool IsEnabled(string flagKey, string identity, bool defaultValue = false);
    bool IsEnabled(string flagKey, IDictionary<string, string> context, bool defaultValue = false);
    bool IsEnabled(string flagKey, string identity, IDictionary<string, string> context, bool defaultValue = false);
    bool IsEnabled<TContext>(string flagKey, TContext contextObject, bool defaultValue = false);
    bool IsEnabled<TContext>(string flagKey, string identity, TContext contextObject, bool defaultValue = false);
    
    bool IsEnabled<TContext>(string flagKey, ref TContext contextObject, bool defaultValue = false) where TContext : ToggleMesh.Common.Contexts.IContextAccessor;
    bool IsEnabled<TContext>(string flagKey, string identity, ref TContext contextObject, bool defaultValue = false) where TContext : ToggleMesh.Common.Contexts.IContextAccessor;
    bool IsEnabled<TContext>(string flagKey, ToggleMeshUser<TContext> user, bool defaultValue = false) where TContext : ToggleMesh.Common.Contexts.IContextAccessor;
    
    void Track(string eventName, string identity, double? value = null);
    void Track<TProperties>(string eventName, string identity, TProperties properties, double? value = null);
    void Track(string eventName, double? value = null);
    void Track<TProperties>(string eventName, TProperties properties, double? value = null);
    void Track<TContext, TProperties>(string eventName, TContext contextObject, TProperties properties, double? value = null);
    void Track<TContext>(string eventName, ToggleMeshUser<TContext> user, double? value = null) where TContext : ToggleMesh.Common.Contexts.IContextAccessor;
    void Track<TContext, TProperties>(string eventName, ToggleMeshUser<TContext> user, TProperties properties, double? value = null) where TContext : ToggleMesh.Common.Contexts.IContextAccessor;
}