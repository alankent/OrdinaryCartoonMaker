using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine.Playables;
using UnityEngine.Sequences.Timeline;
using UnityEngine.Timeline;

// ensure class initializer is called whenever scripts recompile
[InitializeOnLoadAttribute]
public static class PlayModeStateChangedExample
{
    // register an event handler when the class is initialized
    static PlayModeStateChangedExample()
    {
        EditorApplication.playModeStateChanged += LogPlayModeState;
    }

    static void LogPlayModeState(PlayModeStateChange state)
    {
        if (TimeInfo.instance.IsEmpty())
            return;

        if (state == PlayModeStateChange.ExitingEditMode)
        {
            TimelineEditor.masterDirector.playOnAwake = false;
            Selection.activeGameObject = TimelineEditor.masterDirector.gameObject;
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            TimelineEditor.masterDirector.time = TimeInfo.instance.start;
            TimelineEditor.masterDirector.Evaluate();
            TimelineEditor.masterDirector.Play();

            EditorApplication.update += CheckToExitPlayMode;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            TimelineEditor.masterDirector.playOnAwake = TimeInfo.instance.playOnAwake;
            Selection.activeGameObject = TimeInfo.instance.selection;

            TimeInfo.instance.Reset();
        }
    }

    static void CheckToExitPlayMode()
    {
        var end = TimeInfo.instance.start + TimeInfo.instance.duration;
        if (TimelineEditor.masterDirector == null || TimelineEditor.masterDirector.time >= end)
        {
            EditorApplication.update -= CheckToExitPlayMode;

            TimelineEditor.masterDirector.Pause();
            EditorApplication.ExitPlaymode();
        }
    }
}

[Serializable]
class TimeInfo : ScriptableSingleton<TimeInfo>
{
    public double start = -1.0;
    public double duration = -1.0;
    public GameObject selection = null;
    public bool playOnAwake = true;

    public bool IsEmpty()
    {
        return start < 0.0 || duration < 0.0 || selection == null;
    }

    public void Reset()
    {
        start = -1.0;
        duration = -1.0;
        selection = null;
        playOnAwake = true;
    }
}

[MenuEntry("Record", MenuPriority.CustomTimelineActionSection.start)]
public class RecordSequence : TimelineAction
{
    public override bool Execute(ActionContext context)
    {
        var clip = context.clips.First();

        TimeInfo.instance.start = TimelineEditor.GetMasterTimeFromInspectedTime(clip.start);
        TimeInfo.instance.duration = clip.duration;

        TimeInfo.instance.selection = TimelineEditor.inspectedDirector.gameObject;
        TimeInfo.instance.playOnAwake = TimelineEditor.masterDirector.playOnAwake;

        EditorApplication.EnterPlaymode();
        return true;
    }

    public override ActionValidity Validate(ActionContext context)
    {
        if (!context.clips.Any() || context.tracks.Any())
            return ActionValidity.NotApplicable;

        var clip = context.clips.First();
        if (clip.asset != null && clip.asset is EditorialPlayableAsset)
            return ActionValidity.Valid;

        return ActionValidity.Invalid;
    }
}
