//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdmBackwardSolver : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnBase;

  internal FdmBackwardSolver(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwnBase = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdmBackwardSolver obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  ~FdmBackwardSolver() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    global::System.GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnBase) {
          swigCMemOwnBase = false;
          NQuantLibcPINVOKE.delete_FdmBackwardSolver(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public FdmBackwardSolver(FdmLinearOpComposite map, FdmBoundaryConditionSet bcSet, FdmStepConditionComposite condition, FdmSchemeDesc schemeDesc) : this(NQuantLibcPINVOKE.new_FdmBackwardSolver(FdmLinearOpComposite.getCPtr(map), FdmBoundaryConditionSet.getCPtr(bcSet), FdmStepConditionComposite.getCPtr(condition), FdmSchemeDesc.getCPtr(schemeDesc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void rollback(QlArray a, double from, double to, uint steps, uint dampingSteps) {
    NQuantLibcPINVOKE.FdmBackwardSolver_rollback(swigCPtr, QlArray.getCPtr(a), from, to, steps, dampingSteps);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
