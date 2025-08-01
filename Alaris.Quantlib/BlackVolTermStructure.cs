//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class BlackVolTermStructure : VolatilityTermStructure {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal BlackVolTermStructure(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.BlackVolTermStructure_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(BlackVolTermStructure obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_BlackVolTermStructure(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public double blackVol(Date arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVol__SWIG_0(swigCPtr, Date.getCPtr(arg0), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVol(Date arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVol__SWIG_1(swigCPtr, Date.getCPtr(arg0), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVol(double arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVol__SWIG_2(swigCPtr, arg0, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVol(double arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVol__SWIG_3(swigCPtr, arg0, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(Date arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVariance__SWIG_0(swigCPtr, Date.getCPtr(arg0), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(Date arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVariance__SWIG_1(swigCPtr, Date.getCPtr(arg0), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(double arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVariance__SWIG_2(swigCPtr, arg0, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(double arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackVariance__SWIG_3(swigCPtr, arg0, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(Date arg0, Date arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVol__SWIG_0(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(Date arg0, Date arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVol__SWIG_1(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(double arg0, double arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVol__SWIG_2(swigCPtr, arg0, arg1, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(double arg0, double arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVol__SWIG_3(swigCPtr, arg0, arg1, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(Date arg0, Date arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVariance__SWIG_0(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(Date arg0, Date arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVariance__SWIG_1(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(double arg0, double arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVariance__SWIG_2(swigCPtr, arg0, arg1, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(double arg0, double arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructure_blackForwardVariance__SWIG_3(swigCPtr, arg0, arg1, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
