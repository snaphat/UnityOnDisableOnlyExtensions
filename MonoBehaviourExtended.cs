using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.LowLevel;

/*
   This code implements MonoBehaviour extensions for Unity through a MonoBehaviourExtended class. 
   Specifically, it adds support for a custom void OnDisableOnly method, which is called in the same frame that a 
   MonoBehaviourExtended is disabled, but not when it is destroyed (unlike OnDisable). This is facilitated through a 
   custom event manager, OnDisableOnlyEventManager, which handles OnDisableOnly events during the PostLateUpdate phase 
   of the game loop using a custom PlayerLoopSystem.

   The event manager maintains a list of registered OnDisableOnly event handlers for each instance of a class that both 
   extends MonoBehaviourExtended and implements the OnDisableOnly method. The RegisterEvent method of the event 
   manager is called during the Awake phase of the game loop to register the instance to receive OnDisableOnly events.

   During runtime initialization, the event manager registers a custom callback method, PostLateUpdate, in the Unity 
   PlayerLoop system. This callback is executed during the PostLateUpdate phase of the game loop.

   During the PostLateUpdate phase, the event manager iterates over the registered event handlers. 
   It checks if the referenced MonoBehaviourExtended instance is still valid (not null) and compares the cached state of
   its isActiveAndEnabled property with the current state. If there is a change from enabled to disabled, it invokes the
   associated OnDisableOnly method.

   The reason the custom event manager is able to call OnDisableOnly only when an object is disabled but not destroyed 
   is by exploiting the fact that when an object is destroyed via Destroy() or DestroyImmediate(), Unity's overloaded 
   null checks will return that the object is null after the call returns. Using this, the custom event manager can 
   determine if an object was destroyed earlier in the frame during the PostLateUpdate phase. This cannot be determined 
   during the execution of the OnDisable method because it gets called prior to an object being destroyed from within 
   the Destroy or DestroyImmediate calls.
*/

/// <summary>
/// Custom event manager for handling OnDisableOnly events. This manager registers event handlers
/// and executes callbacks when MonoBehaviours are disabled. It operates during the Initialization and
/// PostLateUpdate phases of the PlayerLoop.
/// </summary>
internal static class OnDisableOnlyEventManager
{

    /// <summary>
    /// Structure representing an event handler for the OnDisableOnly event in which the initial isActiveAndEnabled
    /// state of the MonoBehaviour needs to be determined. Used during the Initialization phase of the PlayerLoop.
    /// </summary>
    internal struct InitializationEventHandler
    {
        /// <summary>
        /// Reference to the MonoBehaviour associated with the event handler.
        /// </summary>
        internal MonoBehaviour _behaviour;

        /// <summary>
        /// Callback method to be invoked when the MonoBehaviour is disabled.
        /// </summary>
        internal MethodInfo _onDisableOnlyMethod;
    }

    /// <summary>
    /// Structure representing an event handler for the OnDisableOnly event in which the current isActiveAndEnabled 
    /// state of the MonoBehaviour has been determined. Used during the PostLateUpdate phase of the PlayerLoop.
    /// </summary>
    internal struct OnDisableOnlyEventHandler
    {
        /// <summary>
        /// Reference to the MonoBehaviour associated with the event handler.
        /// </summary>
        internal MonoBehaviour _behaviour;

        /// <summary>
        // Cached value of the isActiveAndEnabled property of the MonoBehaviour
        /// </summary>
        internal bool _cachedIsActiveAndEnabled;

        /// <summary>
        /// Callback method to be invoked when the MonoBehaviour is disabled.
        /// </summary>
        internal MethodInfo _onDisableOnlyMethod;
    }

    /// <summary>
    /// List of all event handlers for the OnDisableOnly event in which the initial isActiveAndEnabled state of the 
    /// MonoBehaviour needs to be determined. Used during the Initialization phase of the PlayerLoop.
    /// </summary>
    private static InitializationEventHandler[] initializationEventHandlers = new InitializationEventHandler[128];

    /// <summary>
    /// Current number of event handlers that are to be initialized this frame.
    /// </summary>
    private static int initializationEventCount = 0;

    /// <summary>
    /// List of all event handlers for the OnDisableOnly event in which the current isActiveAndEnabled state of the 
    /// MonoBehaviour has been determined. Used during the PostLateUpdate phase of the PlayerLoop.
    /// </summary>
    private static OnDisableOnlyEventHandler[] onDisableOnlyEventHandlers = new OnDisableOnlyEventHandler[128];

    /// <summary>
    /// Current number of event handlers that are to be executed this frame.
    /// </summary>
    private static int onDisableOnlyEventCount = 0;

    /// <summary>
    /// Method for adding a new event handler for the OnDisableOnly event. Sets up an event handler for initialization
    /// this frame during the Initialization phase of the PlayerLoop.
    /// </summary>
    /// <param name="behaviour">The MonoBehaviour instance associated with the event handler.</param>
    /// <param name="method">The callback method to be invoked when the MonoBehaviour is disabled.</param>
    internal static void RegisterEvent(MonoBehaviour behaviour, MethodInfo method)
    {
        // Assign to local variables to avoid unnecessary references into class fields
        var eventHandlers = initializationEventHandlers;
        var length = eventHandlers.Length;
        var i = initializationEventCount;

        // Check event handler structure length and resize if necessary
        if (i == length)
        {
            // Resize array since it is too small to hold another handler
            Array.Resize(ref eventHandlers, length * 2);

            // Fix up up handlers reference
            initializationEventHandlers = eventHandlers;
        }

        // Assign handler behaviour, and OnDisableOnly method to call. _cachedIsActiveAndEnabled is false on the initial
        // frame.
        eventHandlers[i]._behaviour = behaviour;
        eventHandlers[i]._onDisableOnlyMethod = method;

        // Increment the field version of event count
        initializationEventCount++;
    }

    /// <summary>
    /// Internal method for initializing and adding an event handler for the OnDisableOnly event. 
    /// It determines the current isActiveAndEnabled state of the MonoBehaviour 
    /// and sets up an event handler for execution during this and subsequent frames of the PlayerLoop.
    /// </summary>
    /// <param name="initializationEventHandler">The initialization event handler containing the MonoBehaviour and the 
    /// OnDisableOnly method to be called.</param>
    private static void InternalRegisterEvent(InitializationEventHandler initializationEventHandler)
    {
        // Assign to local variables to avoid unnecessary references into class fields
        var eventHandlers = onDisableOnlyEventHandlers;
        var length = eventHandlers.Length;
        var i = onDisableOnlyEventCount;

        // Check event handler structure length and resize if necessary
        if (i == length)
        {
            // Resize array since it is too small to hold another handler
            Array.Resize(ref eventHandlers, length * 2);

            // Fix up up handlers reference
            onDisableOnlyEventHandlers = eventHandlers;
        }

        // Assign handler behaviour, current state, and OnDisableOnly method to call
        eventHandlers[i]._behaviour = initializationEventHandler._behaviour;
        eventHandlers[i]._cachedIsActiveAndEnabled = initializationEventHandler._behaviour.isActiveAndEnabled;
        eventHandlers[i]._onDisableOnlyMethod = initializationEventHandler._onDisableOnlyMethod;

        // Increment the field version of event count
        onDisableOnlyEventCount++;
    }


    /// <summary>
    /// Method executed during runtime initialization to register two custom callbacks in the PlayerLoop.
    /// These callbacks are used to initialize new OnDisableOnly event handlers and to execute the event handlers each
    /// frame, respectively.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterPlayerLoopSystems()
    {
        // Retrieve the current PlayerLoop
        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();

        // Find the PostLateUpdate subsystem in the PlayerLoop
        PlayerLoopSystem[] systems = playerLoop.subSystemList;
        for (int i = 0; i < systems.Length; i++)
        {
            // Add Initialization or PostLateUpdate system
            if (systems[i].type == typeof(UnityEngine.PlayerLoop.Initialization) ||
                systems[i].type == typeof(UnityEngine.PlayerLoop.PostLateUpdate))
            {
                // Append the custom callback system to the subsystem
                PlayerLoopSystem[] subSystems = systems[i].subSystemList;
                Array.Resize(ref subSystems, subSystems.Length + 1);
                subSystems[subSystems.Length - 1] = new PlayerLoopSystem()
                {
                    type = typeof(OnDisableOnlyEventManager),
                    updateDelegate = systems[i].type == typeof(UnityEngine.PlayerLoop.Initialization) ? Initialization : PostLateUpdate
                };
                systems[i].subSystemList = subSystems;
            }
        }

        // Set the modified PlayerLoop back to the runtime
        PlayerLoop.SetPlayerLoop(playerLoop);
    }

    /// <summary>
    /// Custom callback method executed during the Initialization phase of the PlayerLoop.
    /// It iterates over all registered event handlers and checks if the associated MonoBehaviour is still valid.
    /// If valid, it calls the InternalRegisterEvent method to initialize and add an event handler for the OnDisableOnly
    /// event.
    /// </summary>
    private static void Initialization()
    {
        // Iterate over all registered event handlers - use span to avoid unnecessary bounds checks
        var eventHandlers = initializationEventHandlers.AsSpan(0, initializationEventCount);
        for (int i = 0; i < eventHandlers.Length; ++i)
        {
            // Check if the associated MonoBehaviour is still valid
            if (eventHandlers[i]._behaviour != null)
                InternalRegisterEvent(eventHandlers[i]);
        }

        // Clear initialization events
        initializationEventCount = 0;
    }

    /// <summary>
    /// Custom callback method executed during the PostLateUpdate phase of the PlayerLoops.
    /// It iterates over all registered event handlers for the OnDisableOnly event.
    /// For each event handler, it checks if the associated MonoBehaviour is still valid and if the 
    /// isActiveAndEnabled property has changed. If the MonoBehaviour is disabled, it invokes the onDisableOnlyMethod of 
    /// the event handler.
    /// </summary>
    private static void PostLateUpdate()
    {
        // Used to keep track of any shifts that need to occur if event handlers are removed
        int shift = -1;

        // Iterate over all registered event handlers - use span to avoid unnecessary bounds checks
        var eventHandlers = onDisableOnlyEventHandlers.AsSpan(0, onDisableOnlyEventCount);
        for (int i = 0; i < eventHandlers.Length; ++i)
        {
            // Check if the associated MonoBehaviour is still valid
            if (eventHandlers[i]._behaviour != null)
            {
                // Check if the isActiveAndEnabled property has changed
                if (eventHandlers[i]._cachedIsActiveAndEnabled != eventHandlers[i]._behaviour.isActiveAndEnabled)
                {
                    // Update the cached isActiveAndEnabled value
                    eventHandlers[i]._cachedIsActiveAndEnabled = eventHandlers[i]._behaviour.isActiveAndEnabled;

                    // If the MonoBehaviour is disabled, invoke the onDisableOnlyMethod
                    if (!eventHandlers[i]._cachedIsActiveAndEnabled)
                        _ = eventHandlers[i]._onDisableOnlyMethod.Invoke(eventHandlers[i]._behaviour, null);
                }
                // Shift the remaining valid event handlers to their next available position
                if (shift > -1)
                {
                    eventHandlers[shift] = eventHandlers[i];
                    ++shift;
                }
            }
            else
            {
                // Mark the current position as invalid and ready for shifting
                shift = i;
            }
        }

        // Update the count of valid event handlers after shifting
        if (shift > -1) onDisableOnlyEventCount = shift;
    }
}

/// <summary>
/// Base class for extended MonoBehaviours.
/// </summary>
public abstract class MonoBehaviourExtended : MonoBehaviour
{
    /// <summary>
    /// Lookup table for caching derived type methods.
    /// </summary>
    private static readonly Dictionary<Type, MethodInfo> _methodCache = new();

    /// <summary>
    /// Method that is called at the beginning of time to look up all methods for all derived types of 
    /// MonoBehaviourExtended using reflection and then caches the results in a lookup table for fast access later.
    /// </summary>
    static MonoBehaviourExtended()
    {
        // Get base MonoBehaviourExtended type
        var baseType = typeof(MonoBehaviourExtended);

        // Find all concrete classes of MonoBehaviourExtended
        foreach (var derivedType in baseType.Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(baseType)))
        {
            // Insert an empty lookup entry for the type
            _methodCache[derivedType] = null;

            // Walk up the hierarchy chain of types to determine if any implement the specified methods
            for (var type = derivedType; type != baseType; type = type.BaseType)
            {
                // Lookup MethodInfo for the given type and cache to avoid costly reflection later
                var onDisableOnlyMethod = type.GetMethod("OnDisableOnly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null);
                if (onDisableOnlyMethod != null)
                {
                    _methodCache[derivedType] = onDisableOnlyMethod;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Awake method for the MonoBehaviour.
    /// </summary>
    protected MonoBehaviourExtended()
    {
        // Retrieve the cached method information
        var onDisableOnlyMethod = _methodCache[GetType()];

        // Register the MonoBehaviour instance as an event handler for the OnDisableOnly event if the class implements
        // the OnDisableOnly method
        if (onDisableOnlyMethod != null) OnDisableOnlyEventManager.RegisterEvent(this, onDisableOnlyMethod);
    }
}
