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

        DateTimeOffset timelineStart = _events[0].Start;
        DateTimeOffset timelineEnd = _events.Last().End;
        TimeSpan timelineDuration = timelineEnd - timelineStart;
        float timelineWidth = 2048;

        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        float textHeight = 128;

        const int timeSteps = 16;
        for (int step = 0; step < timeSteps; ++step) {
            float t = step / (timeSteps - 1f);
            float x = position.x + t * timelineWidth;
            float y = position.y;

            Color backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = new(0, 0, 0, 0);
            GUI.Box(new Rect(x, y, textHeight / 2, textHeight), new GUIContent($"{timelineDuration.TotalSeconds * t:f1}s"), _rectStyle);
            GUI.backgroundColor = backgroundColor;
        }

        position.y += textHeight;

        for (int i = 0; i < _events.Count; ++i) {
            TimelineEvent evt = _events[i];

            double startPercent = (timelineEnd - evt.Start).TotalSeconds / timelineDuration.TotalSeconds;
            double endPercent = (timelineEnd - evt.End).TotalSeconds / timelineDuration.TotalSeconds;
            double startX = (1.0 - startPercent) * timelineWidth;
            double endX = (1.0 - endPercent) * timelineWidth;

            float x = position.x + (float)startX;
            float y = position.y + evt.Depth * 40 + i * 15;
            float width = (float)(endX - startX);
            float height = 30;

            Color backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = SelectUniqueColor(i);
            GUI.Box(new Rect(x, y, width, height), new GUIContent(evt.Name), _rectStyle);
            GUI.backgroundColor = backgroundColor;
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

    private record struct TimelineEvent(string Name, DateTimeOffset Start, DateTimeOffset End, int Depth);
    private static readonly List<TimelineEvent> _events = [];

    private record struct IncompleteTimelineEvent(string Name, DateTimeOffset Start);
    private static readonly Stack<IncompleteTimelineEvent> _eventsStack = [];

    private static readonly GUIStyle _rectStyle = new() {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 14,
        fontStyle = FontStyle.Bold,
        normal = new GUIStyleState {
            background = Texture2D.whiteTexture,
        },
    };
}
