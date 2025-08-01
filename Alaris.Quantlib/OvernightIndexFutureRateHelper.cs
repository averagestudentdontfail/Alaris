//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class OvernightIndexFutureRateHelper : RateHelper {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal OvernightIndexFutureRateHelper(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.OvernightIndexFutureRateHelper_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(OvernightIndexFutureRateHelper obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_OvernightIndexFutureRateHelper(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public OvernightIndexFutureRateHelper(QuoteHandle price, Date valueDate, Date maturityDate, OvernightIndex index, QuoteHandle convexityAdjustment, RateAveraging.Type averagingMethod) : this(NQuantLibcPINVOKE.new_OvernightIndexFutureRateHelper__SWIG_0(QuoteHandle.getCPtr(price), Date.getCPtr(valueDate), Date.getCPtr(maturityDate), OvernightIndex.getCPtr(index), QuoteHandle.getCPtr(convexityAdjustment), (int)averagingMethod), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public OvernightIndexFutureRateHelper(QuoteHandle price, Date valueDate, Date maturityDate, OvernightIndex index, QuoteHandle convexityAdjustment) : this(NQuantLibcPINVOKE.new_OvernightIndexFutureRateHelper__SWIG_1(QuoteHandle.getCPtr(price), Date.getCPtr(valueDate), Date.getCPtr(maturityDate), OvernightIndex.getCPtr(index), QuoteHandle.getCPtr(convexityAdjustment)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public OvernightIndexFutureRateHelper(QuoteHandle price, Date valueDate, Date maturityDate, OvernightIndex index) : this(NQuantLibcPINVOKE.new_OvernightIndexFutureRateHelper__SWIG_2(QuoteHandle.getCPtr(price), Date.getCPtr(valueDate), Date.getCPtr(maturityDate), OvernightIndex.getCPtr(index)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double convexityAdjustment() {
    double ret = NQuantLibcPINVOKE.OvernightIndexFutureRateHelper_convexityAdjustment(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
