using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Pure rules for <c>HerdMode.Follow</c> (sheep, livestock-roster §4 "Sheep follow"): followers keep 2-4 m from the
    /// bell-wether, trail it at 70% speed while it is carried, share one stampede heading when the flock panics, and
    /// re-elect the oldest member 3 s after the leader is gone. EditMode-tested; no scene access.
    /// </summary>
    public static class FlockRules
    {
        /// <summary>True when a follower at <paramref name="followerPos"/> must walk toward the leader.</summary>
        public static bool NeedsToFollow(Vector3 leaderPos, Vector3 followerPos, float maxDistance)
        {
            Vector3 d = leaderPos - followerPos; d.y = 0f;
            return d.magnitude > maxDistance;
        }

        /// <summary>
        /// Where a follower should stand: on the line from the leader toward the follower, at a distance in
        /// [min, max] picked by <paramref name="t01"/>, rotated by <paramref name="spreadDeg"/> so followers fan out.
        /// </summary>
        public static Vector3 FollowPoint(Vector3 leaderPos, Vector3 followerPos, float minDistance, float maxDistance, float t01, float spreadDeg)
        {
            Vector3 back = followerPos - leaderPos; back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) back = Vector3.back;
            back = Quaternion.Euler(0f, spreadDeg, 0f) * back.normalized;
            float r = Mathf.Lerp(minDistance, maxDistance, Mathf.Clamp01(t01));
            return leaderPos + back * r;
        }

        /// <summary>Follower speed: walk when the leader is free, <c>sprint x carriedMult</c> while it is carried.</summary>
        public static float FollowSpeed(float walkSpeed, float sprintSpeed, bool leaderCarried, float carriedMult) =>
            leaderCarried ? sprintSpeed * carriedMult : walkSpeed;

        /// <summary>
        /// Leader index in join order (0 = oldest) or -1 while re-electing. <paramref name="lostAt"/> is when the last
        /// leader left (negative infinity if never).
        /// </summary>
        public static int LeaderIndex(int memberCount, float now, float lostAt, float reelectSeconds)
        {
            if (memberCount <= 0) return -1;
            return now - lostAt < reelectSeconds ? -1 : 0;
        }

        /// <summary>
        /// Stampede heading for a panicking member: the flock's shared heading while it is fresh, otherwise its own
        /// "away from threat" direction (which then becomes the shared heading). Result is flat and normalised.
        /// </summary>
        public static Vector3 StampedeHeading(Vector3 ownAway, Vector3 sharedHeading, bool sharedFresh)
        {
            Vector3 d = sharedFresh && sharedHeading.sqrMagnitude > 1e-4f ? sharedHeading : ownAway;
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        /// <summary>Share of followers within <paramref name="radius"/> of <paramref name="leaderPos"/> (roster AC 4: >= 80% within 10 m).</summary>
        public static float ShareWithin(Vector3 leaderPos, Vector3[] followers, float radius)
        {
            if (followers == null || followers.Length == 0) return 1f;
            int n = 0;
            foreach (var f in followers) { Vector3 d = f - leaderPos; d.y = 0f; if (d.magnitude <= radius) n++; }
            return n / (float)followers.Length;
        }
    }
}
