﻿using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using RankSSpawnHelper.Managers;
using RankSSpawnHelper.Modules;
using RankSSpawnHelper.Windows;
#if RELEASE
using Dalamud.Game.Text.SeStringHandling.Payloads;
#endif

namespace RankSSpawnHelper;

public class SpawnHelper : IDalamudPlugin
{
    private readonly Configuration   _configuration;
    private readonly WindowSystem    _windowSystem;
    private readonly ServiceProvider _serviceProvider;
    private readonly MainWindow      _mainWindow;

    public SpawnHelper(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<DalamudApi>();
        _configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _windowSystem  = new (typeof(SpawnHelper).AssemblyQualifiedName);

        var pluginVersion = Assembly.GetExecutingAssembly()
                                    .GetName()
                                    .Version!.ToString();

        Utils.PluginVersion = pluginVersion;
        DalamudApi.PluginLog.Info($"Version: {Utils.PluginVersion}");

#if RELEASE
        if (_configuration.PluginVersion != pluginVersion)
        {
            Utils.Print([
                new TextPayload($"版本 {pluginVersion} 的更新日志:\n"),
                new UIForegroundPayload(35),
                new TextPayload("  [-] API11\n"),
                new TextPayload("  [-] 修复湿地不会自动召唤蓝闪蝶的BUG\n"),
                new TextPayload("  [-] 修复开ACT的显示跨服人数插件时会导致小助手无法加载的BUG\n"),
                new UIForegroundPayload(0),
            ]);
        }
#endif
        _configuration.PluginVersion = pluginVersion;

        var trackerApi = new TrackerApi();

        var services = new ServiceCollection();
        services.AddSingleton(_configuration);
        services.AddSingleton(_windowSystem);
        services.AddSingleton(trackerApi);
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        InitModule<IModule>();
        InitModule<IUiModule>();

        _mainWindow = new (_serviceProvider);
        var counterWindow = new CounterWindow(_serviceProvider, _configuration);
        var huntMapWindow = new HuntMapWindow(_serviceProvider);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(huntMapWindow);
        _windowSystem.AddWindow(counterWindow);

        PostInitModule<IModule>();
        PostInitModule<IUiModule>();

        pluginInterface.UiBuilder.Draw         += UiBuilderOnDraw;
        pluginInterface.UiBuilder.OpenMainUi   += UiBuilderOnOpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += UiBuilderOnOpenMainUi;
    }

    public void Dispose()
    {
        _windowSystem.RemoveAllWindows();

        _serviceProvider.GetServices<IUiModule>()
                        .ToList()
                        .ForEach(x => x.Shutdown());

        _serviceProvider.GetServices<IModule>()
                        .ToList()
                        .ForEach(x => x.Shutdown());

        _configuration.Save();

        GC.SuppressFinalize(this);
    }

    private void UiBuilderOnOpenMainUi()
        => _mainWindow.Toggle();

    private void UiBuilderOnDraw()
        => _windowSystem.Draw();

    private static void ConfigureServices(IServiceCollection services)
    {
        services.ImplSingleton<IModule, ICommandHandler, CommandHandler>();
        services.ImplSingleton<IModule, IDataManager, DataManger>();
        services.ImplSingleton<IUiModule, IConnectionManager, ConnectionManager>();
        services.ImplSingleton<IUiModule, ICounter, Counter>();
        services.AddSingleton<IUiModule, Misc>();
        services.AddSingleton<IUiModule, Automation>();
    }

    private void InitModule<T>() where T : IModule
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            var serviceName = service.GetType()
                                     .FullName;

            if (!service.Init())
            {
                throw new ($"Failed to init module {serviceName}!");
            }

            DalamudApi.PluginLog.Info($"Module {serviceName} is loaded.");
        }
    }

    private void PostInitModule<T>() where T : IModule
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.PostInit(_serviceProvider);
            }
            catch (Exception e)
            {
                DalamudApi.PluginLog.Error(e, $"Error when calling PostInit for module {service.GetType().FullName}");

                throw;
            }
        }
    }
}

internal static class DependencyInjections
{
    public static void ImplSingleton<TService1, TService2, TImpl>(this IServiceCollection services)
        where TImpl : class, TService1, TService2
        where TService1 : class
        where TService2 : class
    {
        services.AddSingleton<TImpl>();

        services.AddSingleton<TService1>(x => x.GetRequiredService<TImpl>());
        services.AddSingleton<TService2>(x => x.GetRequiredService<TImpl>());
    }
}
