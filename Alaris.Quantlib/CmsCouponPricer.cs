//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class CmsCouponPricer : FloatingRateCouponPricer {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal CmsCouponPricer(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.CmsCouponPricer_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(CmsCouponPricer obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_CmsCouponPricer(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public SwaptionVolatilityStructureHandle swaptionVolatility() {
    SwaptionVolatilityStructureHandle ret = new SwaptionVolatilityStructureHandle(NQuantLibcPINVOKE.CmsCouponPricer_swaptionVolatility(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void setSwaptionVolatility(SwaptionVolatilityStructureHandle v) {
    NQuantLibcPINVOKE.CmsCouponPricer_setSwaptionVolatility__SWIG_0(swigCPtr, SwaptionVolatilityStructureHandle.getCPtr(v));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void setSwaptionVolatility() {
    NQuantLibcPINVOKE.CmsCouponPricer_setSwaptionVolatility__SWIG_1(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
