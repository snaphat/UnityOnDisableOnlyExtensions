# MonoBehaviourExtensions

This repository contains a MonoBehaviour extension for Unity game engine. The extension introduces a new custom method `OnDisableOnly`, that is only called when a `MonoBehaviourExtended` object is disabled, but not when it is destroyed, unlike the built-in `OnDisable` method. 

## Class Overview

This project consists of the following main classes:

1. `OnDisableOnlyEventManager`: This class is responsible for handling `OnDisableOnly` events. It maintains a list of registered `OnDisableOnly` event handlers for each instance of a class that extends `MonoBehaviourExtended` and implements the `OnDisableOnly` method. The `OnDisableOnly` events are processed during the `PostLateUpdate` phase of the Unity game loop.

2. `MonoBehaviourExtended`: This is the base class that can be extended by any MonoBehaviour that wishes to utilize the `OnDisableOnly` functionality. It maintains a lookup table for caching derived type methods, which is initialized during runtime. This class also registers the MonoBehaviour instance as an event handler for the `OnDisableOnly` event in its constructor if the class implements the `OnDisableOnly` method.

## Usage

To use the `OnDisableOnly` functionality, you would need to extend the `MonoBehaviourExtended` class instead of `MonoBehaviour` in your scripts. If your class needs to implement a custom action when it is disabled but not destroyed, you can create an `OnDisableOnly` method.

```csharp
public class ExampleClass : MonoBehaviourExtended
{
    private void OnDisableOnly()
    {
        // Custom code here...
    }
}
```

This `OnDisableOnly` method will be invoked only when the instance is disabled, but not when it is destroyed. The registration for `OnDisableOnly` events is done automatically during the `Awake` phase of the Unity game loop.

## Disclaimer

Please note that the behaviour of the extension is designed to exploit the fact that when an object is destroyed via `Destroy()` or `DestroyImmediate()`, Unity's overloaded null checks will return that the object is null after the call returns. Therefore, the behaviour might not work as expected if Unity changes this behaviour in future releases.

## Contributing

If you have any improvements or features you'd like to add, feel free to create a pull request. If you encounter any bugs or issues, please create an issue in this repository.
