//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FloatingRateCoupon : Coupon {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FloatingRateCoupon(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FloatingRateCoupon_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FloatingRateCoupon obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FloatingRateCoupon(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public Date fixingDate() {
    Date ret = new Date(NQuantLibcPINVOKE.FloatingRateCoupon_fixingDate(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public int fixingDays() {
    int ret = NQuantLibcPINVOKE.FloatingRateCoupon_fixingDays(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public bool isInArrears() {
    bool ret = NQuantLibcPINVOKE.FloatingRateCoupon_isInArrears(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double gearing() {
    double ret = NQuantLibcPINVOKE.FloatingRateCoupon_gearing(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double spread() {
    double ret = NQuantLibcPINVOKE.FloatingRateCoupon_spread(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double indexFixing() {
    double ret = NQuantLibcPINVOKE.FloatingRateCoupon_indexFixing(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double adjustedFixing() {
    double ret = NQuantLibcPINVOKE.FloatingRateCoupon_adjustedFixing(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double convexityAdjustment() {
    double ret = NQuantLibcPINVOKE.FloatingRateCoupon_convexityAdjustment(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double price(YieldTermStructureHandle discountCurve) {
    double ret = NQuantLibcPINVOKE.FloatingRateCoupon_price(swigCPtr, YieldTermStructureHandle.getCPtr(discountCurve));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public InterestRateIndex index() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.FloatingRateCoupon_index(swigCPtr);
    InterestRateIndex ret = (cPtr == global::System.IntPtr.Zero) ? null : new InterestRateIndex(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void setPricer(FloatingRateCouponPricer p) {
    NQuantLibcPINVOKE.FloatingRateCoupon_setPricer(swigCPtr, FloatingRateCouponPricer.getCPtr(p));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
