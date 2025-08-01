//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class OvernightIndexFuture : Instrument {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal OvernightIndexFuture(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.OvernightIndexFuture_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(OvernightIndexFuture obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_OvernightIndexFuture(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public OvernightIndexFuture(OvernightIndex overnightIndex, Date valueDate, Date maturityDate, QuoteHandle convexityAdjustment, RateAveraging.Type averagingMethod) : this(NQuantLibcPINVOKE.new_OvernightIndexFuture__SWIG_0(OvernightIndex.getCPtr(overnightIndex), Date.getCPtr(valueDate), Date.getCPtr(maturityDate), QuoteHandle.getCPtr(convexityAdjustment), (int)averagingMethod), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public OvernightIndexFuture(OvernightIndex overnightIndex, Date valueDate, Date maturityDate, QuoteHandle convexityAdjustment) : this(NQuantLibcPINVOKE.new_OvernightIndexFuture__SWIG_1(OvernightIndex.getCPtr(overnightIndex), Date.getCPtr(valueDate), Date.getCPtr(maturityDate), QuoteHandle.getCPtr(convexityAdjustment)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public OvernightIndexFuture(OvernightIndex overnightIndex, Date valueDate, Date maturityDate) : this(NQuantLibcPINVOKE.new_OvernightIndexFuture__SWIG_2(OvernightIndex.getCPtr(overnightIndex), Date.getCPtr(valueDate), Date.getCPtr(maturityDate)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double convexityAdjustment() {
    double ret = NQuantLibcPINVOKE.OvernightIndexFuture_convexityAdjustment(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
