// From ottergui (https://github.com/Ottermandias/OtterGui)
// Most ImGui widgets with IDisposable interface that automatically destroys them
// when created with using variables.

using System;
using ImGuiNET;

namespace RankSSpawnHelper.Ui.ImRaii;

public static partial class ImRaii
{
    public static IEndObject Group()
    {
        ImGui.BeginGroup();
        return new EndUnconditionally(ImGui.EndGroup, true);
    }

    // Used to avoid tree pops when flag for no push is set.
    private static void Nop() { }

    // Exported interface for RAII.
    public interface IEndObject : IDisposable
    {
        // Empty end object.
        public static readonly IEndObject Empty = new EndConditionally(Nop, false);
        public bool Success { get; }

        public static bool operator true(IEndObject i)
        {
            return i.Success;
        }

        public static bool operator false(IEndObject i)
        {
            return !i.Success;
        }

        public static bool operator !(IEndObject i)
        {
            return !i.Success;
        }

        public static bool operator &(IEndObject i, bool value)
        {
            return i.Success && value;
        }

        public static bool operator |(IEndObject i, bool value)
        {
            return i.Success || value;
        }
    }

    // Use end-function regardless of success.
    // Used by Child, Group and Tooltip.
    private struct EndUnconditionally : IEndObject
    {
        private Action EndAction { get; }
        public bool Success { get; }
        public bool Disposed { get; private set; }

        public EndUnconditionally(Action endAction, bool success)
        {
            EndAction = endAction;
            Success   = success;
            Disposed  = false;
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            EndAction();
            Disposed = true;
        }
    }

    // Use end-function only on success.
    private struct EndConditionally : IEndObject
    {
        private Action EndAction { get; }
        public bool Success { get; }
        public bool Disposed { get; private set; }

        public EndConditionally(Action endAction, bool success)
        {
            EndAction = endAction;
            Success   = success;
            Disposed  = false;
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            if (Success)
                EndAction();
            Disposed = true;
        }
    }
}