//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class BlackIborCouponPricer : IborCouponPricer {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal BlackIborCouponPricer(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.BlackIborCouponPricer_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(BlackIborCouponPricer obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_BlackIborCouponPricer(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public BlackIborCouponPricer(OptionletVolatilityStructureHandle v, BlackIborCouponPricer.TimingAdjustment timingAdjustment, QuoteHandle correlation, OptionalBool useIndexedCoupon) : this(NQuantLibcPINVOKE.new_BlackIborCouponPricer__SWIG_0(OptionletVolatilityStructureHandle.getCPtr(v), (int)timingAdjustment, QuoteHandle.getCPtr(correlation), OptionalBool.getCPtr(useIndexedCoupon)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackIborCouponPricer(OptionletVolatilityStructureHandle v, BlackIborCouponPricer.TimingAdjustment timingAdjustment, QuoteHandle correlation) : this(NQuantLibcPINVOKE.new_BlackIborCouponPricer__SWIG_1(OptionletVolatilityStructureHandle.getCPtr(v), (int)timingAdjustment, QuoteHandle.getCPtr(correlation)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackIborCouponPricer(OptionletVolatilityStructureHandle v, BlackIborCouponPricer.TimingAdjustment timingAdjustment) : this(NQuantLibcPINVOKE.new_BlackIborCouponPricer__SWIG_2(OptionletVolatilityStructureHandle.getCPtr(v), (int)timingAdjustment), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackIborCouponPricer(OptionletVolatilityStructureHandle v) : this(NQuantLibcPINVOKE.new_BlackIborCouponPricer__SWIG_3(OptionletVolatilityStructureHandle.getCPtr(v)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackIborCouponPricer() : this(NQuantLibcPINVOKE.new_BlackIborCouponPricer__SWIG_4(), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public enum TimingAdjustment {
    Black76,
    BivariateLognormal
  }

}
