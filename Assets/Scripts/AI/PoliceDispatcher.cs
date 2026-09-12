using System.Collections.Generic;
using Mirror;
using RobEveryone.Core;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.AI
{
    // Sole subscriber to HomeownerAI.OnAlertRaised -- PoliceAI no longer
    // listens for it directly (confirmed bug/design flaw: every existing
    // officer used to react to every single alert independently, so one
    // break-in pulled the entire police force off patrol at once).
    // Instead, one alert redirects only the single closest officer that's
    // actually free (Patrol state, not already busy with something else),
    // and separately may dispatch a brand new one on top of that, up to a
    // cap that scales with how many players are in the lobby. One
    // hand-placed scene instance (NetworkIdentity, wired in the
    // Inspector) lives in SampleScene as permanent ambient coverage; this
    // only ever adds *more* on top of that baseline, and only while under
    // the cap.
    public class PoliceDispatcher : NetworkBehaviour
    {
        [SerializeField] private GameObject policePrefab;
        [SerializeField] private List<Transform> stationSpawnPoints = new();
        // Proposed defaults, tune by playtesting -- cap = max(base, ceil(
        // playerCount * capPerPlayer)), e.g. 2 base, +1 more per 2 players.
        [SerializeField] private int baseDispatchCap = 2;
        [SerializeField] private float capPerPlayer = 0.5f;
        // Night round (Milestone E) -- more officers dispatchable on top
        // of the player-scaled cap above.
        [SerializeField] private int nightDispatchCapBonus = 2;

        // Every officer currently in the scene, dispatched or the
        // original hand-placed one -- used both to count against the cap
        // and as the pool a fresh alert redirects the closest *available*
        // one from.
        private readonly List<PoliceAI> activeOfficers = new();

        // Seeds the baseline hand-placed officer(s) so they count toward
        // the cap from the start -- otherwise the very first alert could
        // dispatch a full cap's worth of new officers on top of whatever
        // already existed.
        public override void OnStartServer()
        {
            activeOfficers.AddRange(FindObjectsByType<PoliceAI>(FindObjectsSortMode.None));
        }

        private void OnEnable() => HomeownerAI.OnAlertRaised += HandleAlertRaised;
        private void OnDisable() => HomeownerAI.OnAlertRaised -= HandleAlertRaised;

        private void HandleAlertRaised(Vector3 position, PlayerInventory blamed)
        {
            if (!isServer) return;

            activeOfficers.RemoveAll(officer => officer == null);

            // Redirect only the single closest officer that isn't already
            // busy investigating/chasing something else -- everyone else
            // (dispatched or the original) stays on whatever they were
            // already doing instead of all piling onto this one alert.
            PoliceAI closest = FindClosestAvailableOfficer(position);
            closest?.RespondTo(position, blamed);

            if (activeOfficers.Count >= ComputeCap()) return; // at capacity -- the redirect above still happened, just no new officer
            if (policePrefab == null) return;

            Transform spawn = stationSpawnPoints.Count > 0
                ? stationSpawnPoints[Random.Range(0, stationSpawnPoints.Count)]
                : transform;

            GameObject instance = Instantiate(policePrefab, spawn.position, spawn.rotation);
            NetworkServer.Spawn(instance);

            PoliceAI officer = instance.GetComponent<PoliceAI>();
            activeOfficers.Add(officer);
            officer.MarkDispatched(spawn.position); // heads home and despawns once it gives up, instead of patrolling forever
            // This alert already fired on the static event before this
            // officer existed to hear it -- send it straight at what it
            // missed instead of waiting for the next one.
            officer.RespondTo(position, blamed);
        }

        // Patrol-state only -- Respond/Searching/Chase/Returning all mean
        // "already busy with something," so those are left alone rather
        // than pulled off whatever they're doing.
        private PoliceAI FindClosestAvailableOfficer(Vector3 position)
        {
            PoliceAI closest = null;
            float closestDistance = float.MaxValue;

            foreach (PoliceAI officer in activeOfficers)
            {
                if (officer == null || officer.State != PoliceState.Patrol) continue;

                float distance = Vector3.Distance(officer.transform.position, position);
                if (distance >= closestDistance) continue;

                closestDistance = distance;
                closest = officer;
            }

            return closest;
        }

        private int ComputeCap()
        {
            int cap = Mathf.Max(baseDispatchCap, Mathf.CeilToInt(PlayerInventory.AllPlayers.Count * capPerPlayer));
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.IsNightRound) cap += nightDispatchCapBonus;
            return cap;
        }
    }
}
