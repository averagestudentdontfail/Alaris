//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdmHestonHullWhiteSolver : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnBase;

  internal FdmHestonHullWhiteSolver(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwnBase = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdmHestonHullWhiteSolver obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  ~FdmHestonHullWhiteSolver() {
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
          NQuantLibcPINVOKE.delete_FdmHestonHullWhiteSolver(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public FdmHestonHullWhiteSolver(HestonProcess hestonProcess, HullWhiteProcess hwProcess, double corrEquityShortRate, FdmSolverDesc solverDesc, FdmSchemeDesc schemeDesc) : this(NQuantLibcPINVOKE.new_FdmHestonHullWhiteSolver__SWIG_0(HestonProcess.getCPtr(hestonProcess), HullWhiteProcess.getCPtr(hwProcess), corrEquityShortRate, FdmSolverDesc.getCPtr(solverDesc), FdmSchemeDesc.getCPtr(schemeDesc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmHestonHullWhiteSolver(HestonProcess hestonProcess, HullWhiteProcess hwProcess, double corrEquityShortRate, FdmSolverDesc solverDesc) : this(NQuantLibcPINVOKE.new_FdmHestonHullWhiteSolver__SWIG_1(HestonProcess.getCPtr(hestonProcess), HullWhiteProcess.getCPtr(hwProcess), corrEquityShortRate, FdmSolverDesc.getCPtr(solverDesc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double valueAt(double s, double v, double r) {
    double ret = NQuantLibcPINVOKE.FdmHestonHullWhiteSolver_valueAt(swigCPtr, s, v, r);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double thetaAt(double s, double v, double r) {
    double ret = NQuantLibcPINVOKE.FdmHestonHullWhiteSolver_thetaAt(swigCPtr, s, v, r);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double deltaAt(double s, double v, double r, double eps) {
    double ret = NQuantLibcPINVOKE.FdmHestonHullWhiteSolver_deltaAt(swigCPtr, s, v, r, eps);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double gammaAt(double s, double v, double r, double eps) {
    double ret = NQuantLibcPINVOKE.FdmHestonHullWhiteSolver_gammaAt(swigCPtr, s, v, r, eps);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
