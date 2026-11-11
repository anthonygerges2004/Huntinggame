using UnityEngine;

/// <summary>Receives the footstep/landing events baked into the pack's animations.</summary>
public class AnimationEventSink : MonoBehaviour
{
    public void OnFootstep(AnimationEvent e) { }
    public void OnLand(AnimationEvent e) { }
}
