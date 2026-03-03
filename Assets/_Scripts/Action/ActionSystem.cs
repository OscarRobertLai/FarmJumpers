using System;
using System.Collections;
using System.Collections.Generic;

public class ActionSystem : Singleton<ActionSystem>
{
    private List<GameAction> reactions = null;
    public bool IsPerforming { get; private set; } = false;

    private static readonly Dictionary<Type, List<Action<GameAction>>> preSubs = new();
    private static readonly Dictionary<Type, List<Action<GameAction>>> postSubs = new();
    private static readonly Dictionary<Type, Func<GameAction, IEnumerator>> performers = new();

    private static readonly Dictionary<(ReactionTiming timing, Type type, Delegate original), Action<GameAction>> subWrappers = new();

    public void AddReaction(GameAction gameAction)
    {
        reactions?.Add(gameAction);
    }

    private IEnumerator PerformReactions()
    {
        foreach (var reaction in reactions)
            yield return Flow(reaction);
    }

    private void PerformSubscribers(GameAction action, Dictionary<Type, List<Action<GameAction>>> subs)
    {
        var type = action.GetType();
        if (!subs.TryGetValue(type, out var list)) return;

        // Copy to avoid issues if someone subscribes/unsubscribes during iteration
        for (int i = 0, count = list.Count; i < count; i++)
            list[i]?.Invoke(action);
    }

    private IEnumerator PerformPerformer(GameAction action)
    {
        var type = action.GetType();
        if (performers.TryGetValue(type, out var perf))
            yield return perf(action);
    }

    private IEnumerator Flow(GameAction action, Action OnFlowFinished = null)
    {
        reactions = action.PreReactions;
        PerformSubscribers(action, preSubs);
        yield return PerformReactions();

        reactions = action.PerformReactions;
        yield return PerformPerformer(action);
        yield return PerformReactions();

        reactions = action.PostReactions;
        PerformSubscribers(action, postSubs);
        yield return PerformReactions();

        OnFlowFinished?.Invoke();
    }

    public static void AttachPerformer<T>(Func<T, IEnumerator> performer) where T : GameAction
    {
        var type = typeof(T);
        IEnumerator wrapped(GameAction a) => performer((T)a);

        performers[type] = wrapped;
    }

    public static void DeteachPerformer<T>() where T : GameAction
    {
        performers.Remove(typeof(T));
    }

    public static void SubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
    {
        var type = typeof(T);
        var subs = timing == ReactionTiming.PRE ? preSubs : postSubs;

        var key = (timing, type, (Delegate)reaction);
        if (subWrappers.ContainsKey(key))
            return; // already subscribed (prevents duplicates)

        void wrapped(GameAction a) => reaction((T)a);
        subWrappers[key] = wrapped;

        if (!subs.TryGetValue(type, out var list))
        {
            list = new List<Action<GameAction>>();
            subs[type] = list;
        }

        list.Add(wrapped);
    }

    public static void UnsubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
    {
        var type = typeof(T);
        var subs = timing == ReactionTiming.PRE ? preSubs : postSubs;

        var key = (timing, type, (Delegate)reaction);
        if (!subWrappers.TryGetValue(key, out var wrapped))
            return;

        subWrappers.Remove(key);

        if (!subs.TryGetValue(type, out var list))
            return;

        list.Remove(wrapped);

        // optional cleanup
        if (list.Count == 0) subs.Remove(type);
    }
}