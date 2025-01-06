using Microsoft.Extensions.DependencyInjection;

namespace RankSSpawnHelper;

internal interface IModule
{
    public bool ShouldDrawUi => false;

    public bool Init();

    public void Shutdown();

    public void PostInit(ServiceProvider serviceProvider)
    {
    }
}

internal interface IUiModule : IModule
{
    bool IModule. ShouldDrawUi => true;
    public string UiName       { get; }

    public void OnDrawUi();
}
