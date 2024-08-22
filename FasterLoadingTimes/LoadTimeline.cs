using Kingmaker.Utility.DotNetExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FasterLoadingTimes;

public static class LoadTimeline {
    public static void OnGUI() {
        if (_events.Count == 0) {
            return;
        }

        Rect position = GUILayoutUtility.GetLastRect();
        Timeline timeline = new(_events[0].Start, _events.Last().End, 6144);

        GUILayout.BeginVertical();
        GUILayout.Space(2048);

        _draws.Clear();

        position.y += DrawTimes(timeline, position);

        for (int i = 0, drawn = 0; i < _events.Count; ++i) {
            TimelineEvent evt = _events[i];
            if (evt.Depth == 0) {
                position.y += DrawEvent(timeline, evt, position, SelectUniqueColor(drawn++));
            }
        }

        // Note: The performance here is atrocious- need to figure out how to draw things at places in a performant way.

        foreach (TimelineDrawCommand draw in _draws) {
            if (draw.Color.a != 1.0f) {
                continue;
            }
            DrawRect(new(draw.Rect.x - 1, draw.Rect.y - 1, draw.Rect.width + 2, draw.Rect.height + 2), Color.black, null);
            DrawRect(draw.Rect, draw.Color, null);
        }

        foreach (TimelineDrawCommand draw in _draws) {
            int cappedTextLength = Math.Min(draw.Text.Length, (int)(draw.Rect.width / 8));
            if (cappedTextLength == 0) {
                continue;
            }
            DrawRect(draw.Rect, new(0, 0, 0, 0), new GUIContent(draw.Text.Substring(0, cappedTextLength)));
        }

        GUILayout.EndVertical();

    }

    public static void LoadingProcess_ClearEvents() {
        _events.Clear();
        // note: there may still be unresolved events in the stack
    }

    public static void LoadingProcess_PushEvent(string name) {
        Debug.Log($"LoadingProcess_PushEvent({name})");
        _eventsStack.Push(new(name, DateTimeOffset.UtcNow));
    }

    public static void LoadingProcess_PopEvent() {
        Debug.Log($"LoadingProcess_PopEvent() with _eventsStack.Count={_eventsStack.Count}");
        IncompleteTimelineEvent evt = _eventsStack.Pop();
        _events.Add(new(evt.Name, evt.Start, DateTimeOffset.UtcNow, _eventsStack.Count));
    }

    private static float DrawTimes(in Timeline timeline, in Rect position) {
        const float height = 128;
        const float width = height / 2;

        int steps = (int)(timeline.Width / width);

        for (int step = 0; step < steps; ++step) {
            float t = step / (steps - 1f);
            _draws.Add(new(new(position.x + t * timeline.Width, position.y, width, height), new(0, 0, 0, 0), $"{timeline.Duration.TotalSeconds * t:f1}s"));
        }

        return height;
    }

    private static float DrawEvent(in Timeline timeline, in TimelineEvent evt, in Rect position, in Color col) {
        const float height = 30;
        _draws.Add(new(GetRectForEvent(timeline, evt, position, height), col, evt.Name));

        TimelineEvent evtForClosure = evt;
        IEnumerable<TimelineEvent> children = _events.Where(x => x.Depth != 0 && x.End <= evtForClosure.End && x.Start >= evtForClosure.Start);
        foreach (TimelineEvent child in children) {
            Rect positionWithOffset = position with {
                y = position.y + child.Depth * height
            };
            _draws.Add(new(GetRectForEvent(timeline, child, positionWithOffset, height), col, child.Name));
        }

        float spacing = height + (height / 3);
        if (children.Any()) {
            spacing += children.Max(x => x.Depth) * height;
        }

        return spacing;
    }

    private static void DrawRect(in Rect rect, in Color col, GUIContent content) {
        Color backgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = col;
        GUI.Box(rect, content, _rectStyle);
        GUI.backgroundColor = backgroundColor;
    }

    private static Rect GetRectForEvent(in Timeline timeline, in TimelineEvent evt, in Rect position, float height) {
        double startPercent = (timeline.End - evt.Start).TotalSeconds / timeline.Duration.TotalSeconds;
        double endPercent = (timeline.End - evt.End).TotalSeconds / timeline.Duration.TotalSeconds;
        double startX = (1.0 - startPercent) * timeline.Width;
        double endX = (1.0 - endPercent) * timeline.Width;
        return new(position.x + (float)startX, position.y, (float)(endX - startX), height);
    }

    private static Color SelectUniqueColor(int idx) => (idx % 7) switch {
        0 => Color.red,
        1 => Color.green,
        2 => Color.blue,
        3 => Color.yellow,
        4 => Color.cyan,
        5 => Color.magenta,
        6 => Color.gray,
        _ => throw new()
    };

    private record struct Timeline(DateTimeOffset Start, DateTimeOffset End, int Width) {
        public readonly TimeSpan Duration => End - Start;
    }

    private record struct TimelineEvent(string Name, DateTimeOffset Start, DateTimeOffset End, int Depth);
    private static readonly List<TimelineEvent> _events = [];

    private record struct IncompleteTimelineEvent(string Name, DateTimeOffset Start);
    private static readonly Stack<IncompleteTimelineEvent> _eventsStack = [];

    private record struct TimelineDrawCommand(Rect Rect, Color Color, string Text);
    private static readonly List<TimelineDrawCommand> _draws = [];

    private static readonly GUIStyle _rectStyle = new() {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 14,
        fontStyle = FontStyle.Bold,
        normal = new GUIStyleState {
            background = Texture2D.whiteTexture,
        },
        border = new(1, 1, 1, 1)
    };
}
