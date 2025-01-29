using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using RankSSpawnHelper.Managers;

namespace RankSSpawnHelper.Windows;

internal class MainWindow : Window
{
    public const     string          Name = "S怪触发小助手";
    private readonly List<IUiModule> _modules;
    private          int             _selectedTab;

    public MainWindow(ServiceProvider service) : base(Name)
    {
        _modules = service.GetServices<IUiModule>()
                          .Where(i =>
                          {
                              if (!i.ShouldDrawUi)
                              {
                                  return false;
                              }

                              if (!string.IsNullOrWhiteSpace(i.UiName))
                              {
                                  return true;
                              }

                              DalamudApi.PluginLog.Warning($"{i.GetType().Name} should draw ui but UiName is empty");

                              return false;
                          })
                          .ToList();

        var handler = service.GetService<ICommandHandler>() ?? throw new InvalidOperationException("获取 ICommandHandler 失败");

        handler.AddCommand("/shelper",
                           new ((_, _) => Toggle())
                           {
                               HelpMessage = "打开设置菜单",
                               ShowInHelp  = true,
                           });

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new (400, 250),
            MaximumSize = new (1280, 720),
        };
    }

    public override void Draw()
    {
        DrawTabSelection();
        DrawTab();
    }

    private void DrawTabSelection()
    {
        ImGui.BeginGroup();

        ImGui.BeginChild("Child1##Cheese", new (ImGui.GetFrameHeight() * 4.5f, 0), true);

        for (var i = 0; i < _modules.Count; i++)
        {
            if (ImGui.Selectable(_modules[i].UiName + "##Cheese", i == _selectedTab))
            {
                _selectedTab = i;
            }
        }

        ImGui.EndChild();
    }

    private void DrawTab()
    {
        ImGui.SameLine();
        ImGui.BeginChild("Child2##Cheese", new (-1, -1), true);

        _modules[_selectedTab]
            .OnDrawUi();

        ImGui.EndChild();
        ImGui.EndGroup();
    }
}
