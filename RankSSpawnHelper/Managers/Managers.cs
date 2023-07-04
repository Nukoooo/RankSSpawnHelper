using System;

namespace RankSSpawnHelper.Managers;

internal class Managers : IDisposable
{
    public Data Data;
    public Font Font;
    public Socket Socket;

    public Managers()
    {
        Data   = new Data();
        Font   = new Font();
        Socket = new Socket();
    }

    public void Dispose()
    {
        Socket.Dispose();
        Font.Dispose();
    }
}