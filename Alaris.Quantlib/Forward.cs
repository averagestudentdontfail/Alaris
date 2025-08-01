//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class Forward : Instrument {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal Forward(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.Forward_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(Forward obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_Forward(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public Date settlementDate() {
    Date ret = new Date(NQuantLibcPINVOKE.Forward_settlementDate(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public new bool isExpired() {
    bool ret = NQuantLibcPINVOKE.Forward_isExpired(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Calendar calendar() {
    Calendar ret = new Calendar(NQuantLibcPINVOKE.Forward_calendar(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public BusinessDayConvention businessDayConvention() {
    BusinessDayConvention ret = (BusinessDayConvention)NQuantLibcPINVOKE.Forward_businessDayConvention(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public DayCounter dayCounter() {
    DayCounter ret = new DayCounter(NQuantLibcPINVOKE.Forward_dayCounter(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public YieldTermStructureHandle discountCurve() {
    YieldTermStructureHandle ret = new YieldTermStructureHandle(NQuantLibcPINVOKE.Forward_discountCurve(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public YieldTermStructureHandle incomeDiscountCurve() {
    YieldTermStructureHandle ret = new YieldTermStructureHandle(NQuantLibcPINVOKE.Forward_incomeDiscountCurve(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double spotValue() {
    double ret = NQuantLibcPINVOKE.Forward_spotValue(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double spotIncome(YieldTermStructureHandle incomeDiscountCurve) {
    double ret = NQuantLibcPINVOKE.Forward_spotIncome(swigCPtr, YieldTermStructureHandle.getCPtr(incomeDiscountCurve));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double forwardValue() {
    double ret = NQuantLibcPINVOKE.Forward_forwardValue(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public InterestRate impliedYield(double underlyingSpotValue, double forwardValue, Date settlementDate, Compounding compoundingConvention, DayCounter dayCounter) {
    InterestRate ret = new InterestRate(NQuantLibcPINVOKE.Forward_impliedYield(swigCPtr, underlyingSpotValue, forwardValue, Date.getCPtr(settlementDate), (int)compoundingConvention, DayCounter.getCPtr(dayCounter)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
