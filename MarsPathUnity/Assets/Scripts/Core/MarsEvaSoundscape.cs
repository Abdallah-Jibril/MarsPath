using UnityEngine;

namespace MarsPath
{
    internal enum EvaHazard { None, Terrain, Rock, Reserve }

    public sealed class MarsEvaSoundscape : MonoBehaviour
    {
        private const string AudioPath = "Audio/Eva/";
        private const float AmbienceVolume = .23f;
        private const double MetresPerFootfall = .64 / .63 / 2;
        private readonly AudioClip[] footfalls = new AudioClip[8];
        private AudioSource ambience, boots, cues;
        private AudioClip alert, arrival;
        private bool hasDistance, wasWalking, wasArrived;
        private double previousDistance, distanceSinceFootfall;
        private int stepIndex;
        private float nextFootfall, nextAlert;
        private EvaHazard previousHazard;

        internal bool HasClips => ambience.clip != null && alert != null && arrival != null &&
            System.Array.TrueForAll(footfalls, clip => clip != null);
        internal bool AmbiencePlaying => ambience.isPlaying;
        internal int FootfallCount { get; private set; }

        private void Awake()
        {
            ambience = CreateSource();
            ambience.clip = Resources.Load<AudioClip>(AudioPath + "Ambient");
            ambience.loop = true;
            ambience.volume = 0;
            boots = CreateSource();
            cues = CreateSource();
            for (int i = 0; i < footfalls.Length; i++)
                footfalls[i] = Resources.Load<AudioClip>(AudioPath + $"Footstep{i + 1:00}");
            alert = Resources.Load<AudioClip>(AudioPath + "Alert");
            arrival = Resources.Load<AudioClip>(AudioPath + "Arrival");
        }

        private AudioSource CreateSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            return source;
        }

        internal void Mix(bool outside, bool walking, bool arrived, double travelled, EvaHazard hazard, bool radioLive)
        {
            if (outside && !ambience.isPlaying && ambience.clip != null) ambience.Play();
            float mix = radioLive ? .24f : 1f;
            float step = Time.unscaledDeltaTime;
            ambience.volume = Mathf.MoveTowards(ambience.volume, outside ? AmbienceVolume * mix : 0, step * (radioLive ? 2f : .8f));
            boots.volume = Mathf.MoveTowards(boots.volume, mix, step * (radioLive ? 8f : 3f));
            cues.volume = Mathf.MoveTowards(cues.volume, radioLive ? .55f : 1f, step * (radioLive ? 6f : 3f));
            if (!outside)
            {
                if (ambience.volume == 0 && ambience.isPlaying) ambience.Stop();
                if (boots.isPlaying) boots.Stop();
                if (cues.isPlaying) cues.Stop();
                previousHazard = EvaHazard.None;
                wasArrived = false;
                ResetMotion();
                return;
            }

            if (arrived && !wasArrived && wasWalking && arrival != null)
            {
                cues.Stop();
                cues.PlayOneShot(arrival, .48f);
            }
            else if (!arrived && hazard != EvaHazard.None && hazard != previousHazard &&
                     Time.unscaledTime >= nextAlert && alert != null)
            {
                cues.PlayOneShot(alert, .48f);
                nextAlert = Time.unscaledTime + 4f;
            }
            previousHazard = hazard;
            wasArrived = arrived;

            double delta = hasDistance ? travelled - previousDistance : 0;
            previousDistance = travelled;
            hasDistance = true;
            if (walking && delta > 0 && delta < 1.5)
            {
                distanceSinceFootfall += delta;
                if (distanceSinceFootfall >= MetresPerFootfall && Time.unscaledTime >= nextFootfall)
                {
                    distanceSinceFootfall %= MetresPerFootfall;
                    // Relatively prime stride visits all eight timbres before repeating.
                    AudioClip clip = footfalls[(stepIndex * 3) % footfalls.Length];
                    boots.panStereo = (stepIndex & 1) == 0 ? -.11f : .11f;
                    boots.pitch = .97f + (stepIndex % 7) * .009f;
                    if (clip != null)
                    {
                        boots.PlayOneShot(clip, .56f);
                        FootfallCount++;
                    }
                    stepIndex++;
                    nextFootfall = Time.unscaledTime + .24f;
                }
            }
            else if (!walking || delta < 0 || delta >= 1.5)
            {
                distanceSinceFootfall = 0;
                if (boots.isPlaying) boots.Stop();
            }
            wasWalking = walking;
        }

        internal void ResetMotion()
        {
            hasDistance = false;
            wasWalking = false;
            distanceSinceFootfall = 0;
        }
    }
}
