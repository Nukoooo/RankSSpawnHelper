using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace RankSSpawnHelper;

public class EntryPoint : IDalamudPlugin
{
    private readonly Commands _commands;
    private readonly WindowSystem _windowSystem;

    public EntryPoint([RequiredVersion("1.0")] DalamudPluginInterface pi)
    {
        pi.Create<DalamudApi>();
        pi.Create<Plugin>();

        // Load all of our commands
        _commands = new();

        var assembly = Assembly.GetExecutingAssembly();
        var context  = AssemblyLoadContext.GetLoadContext(assembly);
        LoadCosturaAssembles();

        Plugin.Managers      = new();
        Plugin.Configuration = (Configuration)pi.GetPluginConfig() ?? pi.Create<Configuration>();
        Plugin.Features      = new();

        // Initialize the UI
        _windowSystem  = new(typeof(EntryPoint).AssemblyQualifiedName);
        Plugin.Windows = new(ref _windowSystem);

        DalamudApi.Interface.UiBuilder.Draw         += _windowSystem.Draw;
        DalamudApi.Interface.UiBuilder.OpenConfigUi += OpenConfigUi;

        var pluginVersion = assembly.GetName().Version.ToString();
        Plugin.PluginVersion = pluginVersion;
        DalamudApi.PluginLog.Info($"Version: {Plugin.PluginVersion}");

#if RELEASE
        if (Plugin.Configuration.PluginVersion == pluginVersion)
            return;
        Plugin.Configuration.PluginVersion = pluginVersion;
#endif

        Plugin.Print(new List<Payload>
        {
            new TextPayload($"版本 {pluginVersion} 的更新日志:\n"),
            new UIForegroundPayload(35),
            new TextPayload("  [-] NET8\n"),
            new TextPayload("  [-] 修复出货时不会弹详情的bug\n"),
            new UIForegroundPayload(0),
            new TextPayload("今天人类/畜畜/傻逼死绝了吗?"),
        });

        return;

        void LoadCosturaAssembles()
        {
            Span<byte> span = new byte[65536];
            foreach (var text in from name in assembly.GetManifestResourceNames()
                                 where name.StartsWith("costura.") && name.EndsWith(".dll.compressed")
                                 select name)
            {
                using var deflateStream =
                    new DeflateStream(assembly.GetManifestResourceStream(text), CompressionMode.Decompress, false);
                using var memoryStream = new MemoryStream();

                int num;
                while ((num = deflateStream.Read(span)) != 0)
                {
                    Stream stream = memoryStream;
                    var    span2  = span;
                    stream.Write(span2[..num]);
                }

                memoryStream.Position = 0L;
                context.LoadFromStream(memoryStream);
            }
        }
    }

    public string Name => "SpawnHelper";


    private void OpenConfigUi()
    {
        Plugin.Windows.PluginWindow.IsOpen = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _commands.Dispose();

        Plugin.Managers.Data.MapTexture.Dispose();
        Plugin.Configuration.Save();
        Plugin.Managers.Dispose();
        Plugin.Features.Dispose();


        DalamudApi.Interface.UiBuilder.Draw -= _windowSystem.Draw;
        _windowSystem.RemoveAllWindows();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}