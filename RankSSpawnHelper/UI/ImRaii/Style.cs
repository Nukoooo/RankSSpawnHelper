// From ottergui (https://github.com/Ottermandias/OtterGui)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;

// From ottergui (https://github.com/Ottermandias/OtterGui)

namespace RankSSpawnHelper.Ui.ImRaii;

// Push an arbitrary amount of styles into an object that are all popped when it is disposed.
// If condition is false, no style is pushed.
// In debug mode, checks that the type of the value given for the style is valid.
public static partial class ImRaii
{
    public static Style PushStyle(ImGuiStyleVar idx, Vector2 value, bool condition = true)
    {
        return new Style().Push(idx, value, condition);
    }

    public sealed class Style : IDisposable
    {
        internal static readonly List<(ImGuiStyleVar, Vector2)> Stack = new();

        private int _count;

        public void Dispose()
        {
            Pop(_count);
        }

        [Conditional("DEBUG")]
        private static void CheckStyleIdx(ImGuiStyleVar idx, Type type)
        {
            var shouldThrow = idx switch
                              {
                                  ImGuiStyleVar.Alpha               => type != typeof(float),
                                  ImGuiStyleVar.WindowPadding       => type != typeof(Vector2),
                                  ImGuiStyleVar.WindowRounding      => type != typeof(float),
                                  ImGuiStyleVar.WindowBorderSize    => type != typeof(float),
                                  ImGuiStyleVar.WindowMinSize       => type != typeof(Vector2),
                                  ImGuiStyleVar.WindowTitleAlign    => type != typeof(Vector2),
                                  ImGuiStyleVar.ChildRounding       => type != typeof(float),
                                  ImGuiStyleVar.ChildBorderSize     => type != typeof(float),
                                  ImGuiStyleVar.PopupRounding       => type != typeof(float),
                                  ImGuiStyleVar.PopupBorderSize     => type != typeof(float),
                                  ImGuiStyleVar.FramePadding        => type != typeof(Vector2),
                                  ImGuiStyleVar.FrameRounding       => type != typeof(float),
                                  ImGuiStyleVar.FrameBorderSize     => type != typeof(float),
                                  ImGuiStyleVar.ItemSpacing         => type != typeof(Vector2),
                                  ImGuiStyleVar.ItemInnerSpacing    => type != typeof(Vector2),
                                  ImGuiStyleVar.IndentSpacing       => type != typeof(float),
                                  ImGuiStyleVar.CellPadding         => type != typeof(Vector2),
                                  ImGuiStyleVar.ScrollbarSize       => type != typeof(float),
                                  ImGuiStyleVar.ScrollbarRounding   => type != typeof(float),
                                  ImGuiStyleVar.GrabMinSize         => type != typeof(float),
                                  ImGuiStyleVar.GrabRounding        => type != typeof(float),
                                  ImGuiStyleVar.TabRounding         => type != typeof(float),
                                  ImGuiStyleVar.ButtonTextAlign     => type != typeof(Vector2),
                                  ImGuiStyleVar.SelectableTextAlign => type != typeof(Vector2),
                                  ImGuiStyleVar.DisabledAlpha       => type != typeof(float),
                                  _                                 => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
                              };

            if (shouldThrow)
                throw new ArgumentException($"Unable to push {type} to {idx}.");
        }

        public static Vector2 GetStyle(ImGuiStyleVar idx)
        {
            var style = ImGui.GetStyle();
            return idx switch
                   {
                       ImGuiStyleVar.Alpha               => new(style.Alpha, float.NaN),
                       ImGuiStyleVar.WindowPadding       => style.WindowPadding,
                       ImGuiStyleVar.WindowRounding      => new(style.WindowRounding, float.NaN),
                       ImGuiStyleVar.WindowBorderSize    => new(style.WindowBorderSize, float.NaN),
                       ImGuiStyleVar.WindowMinSize       => style.WindowMinSize,
                       ImGuiStyleVar.WindowTitleAlign    => style.WindowTitleAlign,
                       ImGuiStyleVar.ChildRounding       => new(style.ChildRounding, float.NaN),
                       ImGuiStyleVar.ChildBorderSize     => new(style.ChildBorderSize, float.NaN),
                       ImGuiStyleVar.PopupRounding       => new(style.PopupRounding, float.NaN),
                       ImGuiStyleVar.PopupBorderSize     => new(style.PopupBorderSize, float.NaN),
                       ImGuiStyleVar.FramePadding        => style.FramePadding,
                       ImGuiStyleVar.FrameRounding       => new(style.FrameRounding, float.NaN),
                       ImGuiStyleVar.FrameBorderSize     => new(style.FrameBorderSize, float.NaN),
                       ImGuiStyleVar.ItemSpacing         => style.ItemSpacing,
                       ImGuiStyleVar.ItemInnerSpacing    => style.ItemInnerSpacing,
                       ImGuiStyleVar.IndentSpacing       => new(style.IndentSpacing, float.NaN),
                       ImGuiStyleVar.CellPadding         => style.CellPadding,
                       ImGuiStyleVar.ScrollbarSize       => new(style.ScrollbarSize, float.NaN),
                       ImGuiStyleVar.ScrollbarRounding   => new(style.ScrollbarRounding, float.NaN),
                       ImGuiStyleVar.GrabMinSize         => new(style.GrabMinSize, float.NaN),
                       ImGuiStyleVar.GrabRounding        => new(style.GrabRounding, float.NaN),
                       ImGuiStyleVar.TabRounding         => new(style.TabRounding, float.NaN),
                       ImGuiStyleVar.ButtonTextAlign     => style.ButtonTextAlign,
                       ImGuiStyleVar.SelectableTextAlign => style.SelectableTextAlign,
                       ImGuiStyleVar.DisabledAlpha       => new(style.DisabledAlpha, float.NaN),
                       _                                 => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
                   };
        }

        public Style Push(ImGuiStyleVar idx, float value, bool condition = true)
        {
            if (!condition)
                return this;

            CheckStyleIdx(idx, typeof(float));
            Stack.Add((idx, GetStyle(idx)));
            ImGui.PushStyleVar(idx, value);
            ++_count;

            return this;
        }

        public Style Push(ImGuiStyleVar idx, Vector2 value, bool condition = true)
        {
            if (!condition)
                return this;

            CheckStyleIdx(idx, typeof(Vector2));
            Stack.Add((idx, GetStyle(idx)));
            ImGui.PushStyleVar(idx, value);
            ++_count;

            return this;
        }

        public void Pop(int num = 1)
        {
            num    =  Math.Min(num, _count);
            _count -= num;
            ImGui.PopStyleVar(num);
            Stack.RemoveRange(Stack.Count - num, num);
        }
    }
}