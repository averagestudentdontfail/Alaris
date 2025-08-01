//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class NumericHaganPricer : CmsCouponPricer {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal NumericHaganPricer(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.NumericHaganPricer_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(NumericHaganPricer obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_NumericHaganPricer(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public NumericHaganPricer(SwaptionVolatilityStructureHandle v, GFunctionFactory.YieldCurveModel model, QuoteHandle meanReversion, double lowerLimit, double upperLimit, double precision) : this(NQuantLibcPINVOKE.new_NumericHaganPricer__SWIG_0(SwaptionVolatilityStructureHandle.getCPtr(v), (int)model, QuoteHandle.getCPtr(meanReversion), lowerLimit, upperLimit, precision), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NumericHaganPricer(SwaptionVolatilityStructureHandle v, GFunctionFactory.YieldCurveModel model, QuoteHandle meanReversion, double lowerLimit, double upperLimit) : this(NQuantLibcPINVOKE.new_NumericHaganPricer__SWIG_1(SwaptionVolatilityStructureHandle.getCPtr(v), (int)model, QuoteHandle.getCPtr(meanReversion), lowerLimit, upperLimit), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NumericHaganPricer(SwaptionVolatilityStructureHandle v, GFunctionFactory.YieldCurveModel model, QuoteHandle meanReversion, double lowerLimit) : this(NQuantLibcPINVOKE.new_NumericHaganPricer__SWIG_2(SwaptionVolatilityStructureHandle.getCPtr(v), (int)model, QuoteHandle.getCPtr(meanReversion), lowerLimit), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NumericHaganPricer(SwaptionVolatilityStructureHandle v, GFunctionFactory.YieldCurveModel model, QuoteHandle meanReversion) : this(NQuantLibcPINVOKE.new_NumericHaganPricer__SWIG_3(SwaptionVolatilityStructureHandle.getCPtr(v), (int)model, QuoteHandle.getCPtr(meanReversion)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
