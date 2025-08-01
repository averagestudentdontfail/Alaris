//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FxSwapRateHelper : RateHelper {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FxSwapRateHelper(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FxSwapRateHelper_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FxSwapRateHelper obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FxSwapRateHelper(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FxSwapRateHelper(QuoteHandle fwdPoint, QuoteHandle spotFx, Period tenor, uint fixingDays, Calendar calendar, BusinessDayConvention convention, bool endOfMonth, bool isFxBaseCurrencyCollateralCurrency, YieldTermStructureHandle collateralCurve, Calendar tradingCalendar) : this(NQuantLibcPINVOKE.new_FxSwapRateHelper__SWIG_0(QuoteHandle.getCPtr(fwdPoint), QuoteHandle.getCPtr(spotFx), Period.getCPtr(tenor), fixingDays, Calendar.getCPtr(calendar), (int)convention, endOfMonth, isFxBaseCurrencyCollateralCurrency, YieldTermStructureHandle.getCPtr(collateralCurve), Calendar.getCPtr(tradingCalendar)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FxSwapRateHelper(QuoteHandle fwdPoint, QuoteHandle spotFx, Period tenor, uint fixingDays, Calendar calendar, BusinessDayConvention convention, bool endOfMonth, bool isFxBaseCurrencyCollateralCurrency, YieldTermStructureHandle collateralCurve) : this(NQuantLibcPINVOKE.new_FxSwapRateHelper__SWIG_1(QuoteHandle.getCPtr(fwdPoint), QuoteHandle.getCPtr(spotFx), Period.getCPtr(tenor), fixingDays, Calendar.getCPtr(calendar), (int)convention, endOfMonth, isFxBaseCurrencyCollateralCurrency, YieldTermStructureHandle.getCPtr(collateralCurve)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public static FxSwapRateHelper forDates(QuoteHandle fwdPoint, QuoteHandle spotFx, Date startDate, Date endDate, bool isFxBaseCurrencyCollateralCurrency, YieldTermStructureHandle collateralCurve) {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.FxSwapRateHelper_forDates(QuoteHandle.getCPtr(fwdPoint), QuoteHandle.getCPtr(spotFx), Date.getCPtr(startDate), Date.getCPtr(endDate), isFxBaseCurrencyCollateralCurrency, YieldTermStructureHandle.getCPtr(collateralCurve));
    FxSwapRateHelper ret = (cPtr == global::System.IntPtr.Zero) ? null : new FxSwapRateHelper(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double spot() {
    double ret = NQuantLibcPINVOKE.FxSwapRateHelper_spot(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Period tenor() {
    Period ret = new Period(NQuantLibcPINVOKE.FxSwapRateHelper_tenor(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public uint fixingDays() {
    uint ret = NQuantLibcPINVOKE.FxSwapRateHelper_fixingDays(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Calendar calendar() {
    Calendar ret = new Calendar(NQuantLibcPINVOKE.FxSwapRateHelper_calendar(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public BusinessDayConvention businessDayConvention() {
    BusinessDayConvention ret = (BusinessDayConvention)NQuantLibcPINVOKE.FxSwapRateHelper_businessDayConvention(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public bool endOfMonth() {
    bool ret = NQuantLibcPINVOKE.FxSwapRateHelper_endOfMonth(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public bool isFxBaseCurrencyCollateralCurrency() {
    bool ret = NQuantLibcPINVOKE.FxSwapRateHelper_isFxBaseCurrencyCollateralCurrency(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Calendar tradingCalendar() {
    Calendar ret = new Calendar(NQuantLibcPINVOKE.FxSwapRateHelper_tradingCalendar(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Calendar adjustmentCalendar() {
    Calendar ret = new Calendar(NQuantLibcPINVOKE.FxSwapRateHelper_adjustmentCalendar(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
