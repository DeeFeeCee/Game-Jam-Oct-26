using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace GameJam.UI
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class MenuInputScope : MonoBehaviour
    {
        public InputActionAsset inputActions;
        public InputActionAsset RuntimeActions { get; private set; }
        public event Action CancelPressed;
        private InputSystemUIInputModule module;
        private InputAction cancel;
        private readonly List<InputActionReference> references=new List<InputActionReference>();
        private void OnEnable()
        {
            module=GetComponent<InputSystemUIInputModule>();module.enabled=false;
            if(inputActions==null){Debug.LogError("Assign menu input actions.",this);enabled=false;return;}
            RuntimeActions=Instantiate(inputActions);
            RuntimeActions.Disable();module.actionsAsset=RuntimeActions;
            module.move=Reference("Navigate");module.submit=Reference("Submit");module.cancel=Reference("Cancel");
            module.point=Reference("Point");module.leftClick=Reference("Click");module.rightClick=Reference("RightClick");
            module.middleClick=Reference("MiddleClick");module.scrollWheel=Reference("ScrollWheel");
            cancel=RuntimeActions.FindAction("UI/Cancel",true);cancel.performed+=OnCancel;
            RuntimeActions.FindActionMap("UI",true).Enable();module.enabled=true;
        }
        private InputActionReference Reference(string name)
        {
            var reference=InputActionReference.Create(RuntimeActions.FindAction("UI/"+name,true));references.Add(reference);return reference;
        }
        private void OnCancel(InputAction.CallbackContext context)=>CancelPressed?.Invoke();
        private void OnDisable()
        {
            if(cancel!=null) cancel.performed-=OnCancel;cancel=null;
            if(module!=null){module.enabled=false;module.actionsAsset=null;}
            if(RuntimeActions!=null){RuntimeActions.Disable();Destroy(RuntimeActions);RuntimeActions=null;}
            foreach(var reference in references) if(reference!=null)Destroy(reference);references.Clear();
        }
    }
}
