//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class HullWhiteForwardProcess : StochasticProcess1D {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal HullWhiteForwardProcess(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.HullWhiteForwardProcess_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(HullWhiteForwardProcess obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_HullWhiteForwardProcess(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public HullWhiteForwardProcess(YieldTermStructureHandle riskFreeTS, double a, double sigma) : this(NQuantLibcPINVOKE.new_HullWhiteForwardProcess(YieldTermStructureHandle.getCPtr(riskFreeTS), a, sigma), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double alpha(double t) {
    double ret = NQuantLibcPINVOKE.HullWhiteForwardProcess_alpha(swigCPtr, t);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double M_T(double s, double t, double T) {
    double ret = NQuantLibcPINVOKE.HullWhiteForwardProcess_M_T(swigCPtr, s, t, T);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double B(double t, double T) {
    double ret = NQuantLibcPINVOKE.HullWhiteForwardProcess_B(swigCPtr, t, T);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void setForwardMeasureTime(double t) {
    NQuantLibcPINVOKE.HullWhiteForwardProcess_setForwardMeasureTime(swigCPtr, t);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
