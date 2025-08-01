//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class LMMDriftCalculator : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal LMMDriftCalculator(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(LMMDriftCalculator obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(LMMDriftCalculator obj) {
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

  ~LMMDriftCalculator() {
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
          NQuantLibcPINVOKE.delete_LMMDriftCalculator(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public LMMDriftCalculator(Matrix pseudo, DoubleVector displacements, DoubleVector taus, uint numeraire, uint alive) : this(NQuantLibcPINVOKE.new_LMMDriftCalculator(Matrix.getCPtr(pseudo), DoubleVector.getCPtr(displacements), DoubleVector.getCPtr(taus), numeraire, alive), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void compute(LMMCurveState cs, DoubleVector drifts) {
    NQuantLibcPINVOKE.LMMDriftCalculator_compute__SWIG_0(swigCPtr, LMMCurveState.getCPtr(cs), DoubleVector.getCPtr(drifts));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void compute(DoubleVector fwds, DoubleVector drifts) {
    NQuantLibcPINVOKE.LMMDriftCalculator_compute__SWIG_1(swigCPtr, DoubleVector.getCPtr(fwds), DoubleVector.getCPtr(drifts));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void computePlain(LMMCurveState cs, DoubleVector drifts) {
    NQuantLibcPINVOKE.LMMDriftCalculator_computePlain__SWIG_0(swigCPtr, LMMCurveState.getCPtr(cs), DoubleVector.getCPtr(drifts));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void computePlain(DoubleVector fwds, DoubleVector drifts) {
    NQuantLibcPINVOKE.LMMDriftCalculator_computePlain__SWIG_1(swigCPtr, DoubleVector.getCPtr(fwds), DoubleVector.getCPtr(drifts));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void computeReduced(LMMCurveState cs, DoubleVector drifts) {
    NQuantLibcPINVOKE.LMMDriftCalculator_computeReduced__SWIG_0(swigCPtr, LMMCurveState.getCPtr(cs), DoubleVector.getCPtr(drifts));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void computeReduced(DoubleVector fwds, DoubleVector drifts) {
    NQuantLibcPINVOKE.LMMDriftCalculator_computeReduced__SWIG_1(swigCPtr, DoubleVector.getCPtr(fwds), DoubleVector.getCPtr(drifts));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
