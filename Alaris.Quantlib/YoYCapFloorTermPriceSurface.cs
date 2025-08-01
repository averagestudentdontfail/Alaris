//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class YoYCapFloorTermPriceSurface : TermStructure {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal YoYCapFloorTermPriceSurface(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(YoYCapFloorTermPriceSurface obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_YoYCapFloorTermPriceSurface(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public virtual PairDoubleVector atmYoYSwapTimeRates() {
    PairDoubleVector ret = new PairDoubleVector(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYSwapTimeRates(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual SWIGTYPE_p_std__pairT_std__vectorT_Date_t_std__vectorT_double_t_t atmYoYSwapDateRates() {
    SWIGTYPE_p_std__pairT_std__vectorT_Date_t_std__vectorT_double_t_t ret = new SWIGTYPE_p_std__pairT_std__vectorT_Date_t_std__vectorT_double_t_t(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYSwapDateRates(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual YoYInflationTermStructure YoYTS() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_YoYTS(swigCPtr);
    YoYInflationTermStructure ret = (cPtr == global::System.IntPtr.Zero) ? null : new YoYInflationTermStructure(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public YoYInflationIndex yoyIndex() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_yoyIndex(swigCPtr);
    YoYInflationIndex ret = (cPtr == global::System.IntPtr.Zero) ? null : new YoYInflationIndex(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual BusinessDayConvention businessDayConvention() {
    BusinessDayConvention ret = (BusinessDayConvention)NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_businessDayConvention(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual Period observationLag() {
    Period ret = new Period(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_observationLag(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual Frequency frequency() {
    Frequency ret = (Frequency)NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_frequency(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual uint fixingDays() {
    uint ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_fixingDays(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double price(Date d, double k) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_price__SWIG_0(swigCPtr, Date.getCPtr(d), k);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double capPrice(Date d, double k) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_capPrice__SWIG_0(swigCPtr, Date.getCPtr(d), k);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double floorPrice(Date d, double k) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_floorPrice__SWIG_0(swigCPtr, Date.getCPtr(d), k);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYSwapRate(Date d, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYSwapRate__SWIG_0(swigCPtr, Date.getCPtr(d), extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYSwapRate(Date d) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYSwapRate__SWIG_1(swigCPtr, Date.getCPtr(d));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYRate(Date d, Period obsLag, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYRate__SWIG_0(swigCPtr, Date.getCPtr(d), Period.getCPtr(obsLag), extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYRate(Date d, Period obsLag) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYRate__SWIG_1(swigCPtr, Date.getCPtr(d), Period.getCPtr(obsLag));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYRate(Date d) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYRate__SWIG_2(swigCPtr, Date.getCPtr(d));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual Date baseDate() {
    Date ret = new Date(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_baseDate(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double price(Period d, double k) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_price__SWIG_1(swigCPtr, Period.getCPtr(d), k);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double capPrice(Period d, double k) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_capPrice__SWIG_1(swigCPtr, Period.getCPtr(d), k);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double floorPrice(Period d, double k) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_floorPrice__SWIG_1(swigCPtr, Period.getCPtr(d), k);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYSwapRate(Period d, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYSwapRate__SWIG_2(swigCPtr, Period.getCPtr(d), extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYSwapRate(Period d) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYSwapRate__SWIG_3(swigCPtr, Period.getCPtr(d));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYRate(Period d, Period obsLag, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYRate__SWIG_3(swigCPtr, Period.getCPtr(d), Period.getCPtr(obsLag), extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYRate(Period d, Period obsLag) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYRate__SWIG_4(swigCPtr, Period.getCPtr(d), Period.getCPtr(obsLag));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double atmYoYRate(Period d) {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_atmYoYRate__SWIG_5(swigCPtr, Period.getCPtr(d));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual DoubleVector strikes() {
    DoubleVector ret = new DoubleVector(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_strikes(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual DoubleVector capStrikes() {
    DoubleVector ret = new DoubleVector(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_capStrikes(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual DoubleVector floorStrikes() {
    DoubleVector ret = new DoubleVector(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_floorStrikes(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual PeriodVector maturities() {
    PeriodVector ret = new PeriodVector(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_maturities(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double minStrike() {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_minStrike(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual double maxStrike() {
    double ret = NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_maxStrike(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual Date minMaturity() {
    Date ret = new Date(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_minMaturity(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual Date maxMaturity() {
    Date ret = new Date(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_maxMaturity(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public virtual Date yoyOptionDateFromTenor(Period p) {
    Date ret = new Date(NQuantLibcPINVOKE.YoYCapFloorTermPriceSurface_yoyOptionDateFromTenor(swigCPtr, Period.getCPtr(p)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
