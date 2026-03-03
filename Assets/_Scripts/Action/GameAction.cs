using System.Collections.Generic;
using UnityEngine;

public class GameAction : MonoBehaviour
{
    public List<GameAction> PreReactions {get; private set; } = new();
    public List<GameAction> PerformReactions {get; private set; } = new();
    public List<GameAction> PostReactions {get; private set; } = new();   
}