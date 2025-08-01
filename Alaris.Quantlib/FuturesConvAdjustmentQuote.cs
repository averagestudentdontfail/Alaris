//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FuturesConvAdjustmentQuote : Quote {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FuturesConvAdjustmentQuote(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FuturesConvAdjustmentQuote_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FuturesConvAdjustmentQuote obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FuturesConvAdjustmentQuote(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FuturesConvAdjustmentQuote(SWIGTYPE_p_ext__shared_ptrT_IborIndex_t index, Date futuresDate, QuoteHandle futuresQuote, QuoteHandle volatility, QuoteHandle meanReversion) : this(NQuantLibcPINVOKE.new_FuturesConvAdjustmentQuote__SWIG_0(SWIGTYPE_p_ext__shared_ptrT_IborIndex_t.getCPtr(index), Date.getCPtr(futuresDate), QuoteHandle.getCPtr(futuresQuote), QuoteHandle.getCPtr(volatility), QuoteHandle.getCPtr(meanReversion)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FuturesConvAdjustmentQuote(SWIGTYPE_p_ext__shared_ptrT_IborIndex_t index, string immCode, QuoteHandle futuresQuote, QuoteHandle volatility, QuoteHandle meanReversion) : this(NQuantLibcPINVOKE.new_FuturesConvAdjustmentQuote__SWIG_1(SWIGTYPE_p_ext__shared_ptrT_IborIndex_t.getCPtr(index), immCode, QuoteHandle.getCPtr(futuresQuote), QuoteHandle.getCPtr(volatility), QuoteHandle.getCPtr(meanReversion)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double futuresValue() {
    double ret = NQuantLibcPINVOKE.FuturesConvAdjustmentQuote_futuresValue(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double volatility() {
    double ret = NQuantLibcPINVOKE.FuturesConvAdjustmentQuote_volatility(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double meanReversion() {
    double ret = NQuantLibcPINVOKE.FuturesConvAdjustmentQuote_meanReversion(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Date immDate() {
    Date ret = new Date(NQuantLibcPINVOKE.FuturesConvAdjustmentQuote_immDate(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
