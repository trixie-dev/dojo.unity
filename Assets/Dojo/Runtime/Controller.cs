using System;
using System.Threading.Tasks;
using bottlenoselabs.C2CS.Runtime;
using dojo_bindings;
using UnityEngine;

namespace Dojo.Starknet
{
    public unsafe class Controller
    {
        private dojo.Controller* controller;
        public FieldElement Address => new FieldElement(dojo.controller_address(controller));
        public FieldElement ChainId => new FieldElement(dojo.controller_chain_id(controller));
        public string Username => dojo.controller_username(controller);

        private Controller(dojo.Controller* controller)
        {
            this.controller = controller;
        }

        public static Controller GetAccount(Policy[] policies, FieldElement chainId)
        {
            dojo.Policy* policiesPtr = null;
            if (policies.Length > 0)
            {
                fixed (dojo.Policy* ptr = &policies[0])
                {
                    policiesPtr = ptr;
                }
            }

            var result = dojo.controller_account(policiesPtr, (UIntPtr)policies.Length, chainId.Inner);
            if (result.tag == dojo.ResultController_Tag.ErrController)
            {
                throw new Exception(result.err.message);
            }

            return new Controller(result._ok);
        }

        public static async Task<Controller> Connect(string rpcUrl, Policy[] policies)
        {
            var connectionTask = new TaskCompletionSource<Controller>();
            CString crpcUrl = CString.FromString(rpcUrl);

            dojo.Policy* policiesPtr = null;
            if (policies.Length > 0)
            {
                fixed (dojo.Policy* ptr = &policies[0])
                {
                    policiesPtr = ptr;
                }
            }

            var onConnect = new dojo.FnPtr_ControllerPtr_Void((controllerPtr) =>
            {
                var controller = new Controller(controllerPtr);
                connectionTask.SetResult(controller);
            });

            dojo.controller_connect(crpcUrl, policiesPtr, (UIntPtr)policies.Length, onConnect);

            return await connectionTask.Task;
        }

        public static bool Clear(Policy[] policies, FieldElement chainId)
        {
            dojo.Policy* policiesPtr = null;
            if (policies.Length > 0)
            {
                fixed (dojo.Policy* ptr = &policies[0])
                {
                    policiesPtr = ptr;
                }
            }

            var result = dojo.controller_clear(policiesPtr, (UIntPtr)policies.Length, chainId.Inner);
            if (result.tag == dojo.Resultbool_Tag.Errbool)
            {
                throw new Exception(result.err.message);
            }

            return result.ok;
        }

        public FieldElement ExecuteRaw(dojo.Call[] calls)
        {
            dojo.Call* callsPtr;
            fixed (dojo.Call* ptr = &calls[0])
            {
                callsPtr = ptr;
            }

            var result = dojo.controller_execute_raw(controller, callsPtr, (UIntPtr)calls.Length);
            if (result.tag == dojo.ResultFieldElement_Tag.ErrFieldElement)
            {
                throw new Exception(result.err.message);
            }

            return new FieldElement(result.ok);
        }

        public FieldElement ExecuteFromOutside(dojo.Call[] calls)
        {
            dojo.Call* callsPtr;
            fixed (dojo.Call* ptr = &calls[0])
            {
                callsPtr = ptr;
            }

            var result = dojo.controller_execute_from_outside(controller, callsPtr, (UIntPtr)calls.Length);
            if (result.tag == dojo.ResultFieldElement_Tag.ErrFieldElement)
            {
                throw new Exception(result.err.message);
            }

            return new FieldElement(result.ok);
        }

        public FieldElement Nonce()
        {
            if (controller == null)
            {
                throw new InvalidOperationException("Controller is not initialized");
            }

            var result = dojo.controller_nonce(controller);
            if (result.tag == dojo.ResultFieldElement_Tag.ErrFieldElement)
            {
                throw new Exception(result.err.message);
            }

            return new FieldElement(result.ok);
        }
    }
}