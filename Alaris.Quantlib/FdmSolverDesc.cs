//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdmSolverDesc : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal FdmSolverDesc(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdmSolverDesc obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(FdmSolverDesc obj) {
    if (obj != null) {
      if (!obj.swigCMemOwn)
        throw new global::System.ApplicationException("Cannot release ownership as memory is not owned");
      global::System.Runtime.InteropServices.HandleRef ptr = obj.swigCPtr;
      obj.swigCMemOwn = false;
      obj.Dispose();
      return ptr;
    } else {
      return new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
    }
  }

  ~FdmSolverDesc() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    global::System.GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwn) {
          swigCMemOwn = false;
          NQuantLibcPINVOKE.delete_FdmSolverDesc(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public FdmSolverDesc(FdmMesher mesher, FdmBoundaryConditionSet bcSet, FdmStepConditionComposite condition, FdmInnerValueCalculator calculator, double maturity, uint timeSteps, uint dampingSteps) : this(NQuantLibcPINVOKE.new_FdmSolverDesc(FdmMesher.getCPtr(mesher), FdmBoundaryConditionSet.getCPtr(bcSet), FdmStepConditionComposite.getCPtr(condition), FdmInnerValueCalculator.getCPtr(calculator), maturity, timeSteps, dampingSteps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmMesher getMesher() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.FdmSolverDesc_getMesher(swigCPtr);
    FdmMesher ret = (cPtr == global::System.IntPtr.Zero) ? null : new FdmMesher(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public FdmBoundaryConditionSet getBcSet() {
    FdmBoundaryConditionSet ret = new FdmBoundaryConditionSet(NQuantLibcPINVOKE.FdmSolverDesc_getBcSet(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public FdmStepConditionComposite getStepConditions() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.FdmSolverDesc_getStepConditions(swigCPtr);
    FdmStepConditionComposite ret = (cPtr == global::System.IntPtr.Zero) ? null : new FdmStepConditionComposite(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public FdmInnerValueCalculator getCalculator() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.FdmSolverDesc_getCalculator(swigCPtr);
    FdmInnerValueCalculator ret = (cPtr == global::System.IntPtr.Zero) ? null : new FdmInnerValueCalculator(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double getMaturity() {
    double ret = NQuantLibcPINVOKE.FdmSolverDesc_getMaturity(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public uint getTimeSteps() {
    uint ret = NQuantLibcPINVOKE.FdmSolverDesc_getTimeSteps(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public uint getDampingSteps() {
    uint ret = NQuantLibcPINVOKE.FdmSolverDesc_getDampingSteps(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
