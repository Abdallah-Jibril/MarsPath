using UnityEngine;

namespace MarsPath
{
    /// <summary>Horizontal heading gate for controller-driven Quest traversal.</summary>
    internal sealed class MarsRouteFacing
    {
        internal const float StopDegrees = 75, ResumeDegrees = 50;
        internal bool Blocked { get; private set; }
        internal bool HasHeading { get; private set; }
        internal float SignedError { get; private set; }
        internal bool RouteBehind => HasHeading && Mathf.Abs(SignedError) >= 135;
        internal string Title => RouteBehind ? "ROUTE BEHIND YOU" : "FACE THE ROUTE";
        internal string Instruction => !HasHeading ? "LOOK AHEAD · WALK LOCKED"
            : RouteBehind ? "TURN AROUND · WALK LOCKED"
            : $"TURN {(SignedError < 0 ? "LEFT" : "RIGHT")} {Mathf.Abs(SignedError):0}° · WALK LOCKED";

        internal void Update(bool hasHeading, float signedError)
        {
            HasHeading = hasHeading && !float.IsNaN(signedError) && !float.IsInfinity(signedError);
            SignedError = HasHeading ? Mathf.DeltaAngle(0, signedError) : 0;
            Blocked = !HasHeading || Mathf.Abs(SignedError) > (Blocked ? ResumeDegrees : StopDegrees);
        }

        internal void Reset() { Blocked = false; HasHeading = false; SignedError = 0; }

        internal static bool TrySignedError(Vector3 viewForward, Vector3 routeForward, out float degrees)
        {
            viewForward.y = routeForward.y = 0;
            degrees = 0;
            if (!(viewForward.sqrMagnitude > .01f) || !(routeForward.sqrMagnitude > .01f)) return false;
            degrees = Vector3.SignedAngle(viewForward, routeForward, Vector3.up);
            return !float.IsNaN(degrees) && !float.IsInfinity(degrees);
        }
    }
}
