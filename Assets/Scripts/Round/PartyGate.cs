using System;
using System.Collections.Generic;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    // Waits for every present player to arrive before letting something
    // happen -- ExitPoint uses this so reaching the exit doesn't end the
    // round the instant the first player walks in, it waits until
    // everyone's gathered (e.g. at the taxi). Solo play resolves this the
    // instant its one player arrives (the roster is always exactly one),
    // but the waiting structure is here now so multiplayer only needs a
    // real player roster, not a rewrite of this gathering logic.
    public class PartyGate : MonoBehaviour
    {
        private readonly HashSet<PlayerInventory> arrived = new();

        public event Action OnAllArrived;

        public void NotifyArrived(PlayerInventory player)
        {
            arrived.Add(player);

            int totalPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None).Length;
            if (arrived.Count < totalPlayers) return;

            arrived.Clear();
            OnAllArrived?.Invoke();
        }
    }
}
