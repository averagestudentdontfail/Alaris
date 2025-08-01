//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class ZeroCouponSwap : Swap {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal ZeroCouponSwap(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.ZeroCouponSwap_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(ZeroCouponSwap obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_ZeroCouponSwap(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public ZeroCouponSwap(Swap.Type type, double baseNominal, Date startDate, Date maturityDate, double fixedPayment, IborIndex iborIndex, Calendar paymentCalendar, BusinessDayConvention paymentConvention, uint paymentDelay) : this(NQuantLibcPINVOKE.new_ZeroCouponSwap__SWIG_0((int)type, baseNominal, Date.getCPtr(startDate), Date.getCPtr(maturityDate), fixedPayment, IborIndex.getCPtr(iborIndex), Calendar.getCPtr(paymentCalendar), (int)paymentConvention, paymentDelay), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZeroCouponSwap(Swap.Type type, double baseNominal, Date startDate, Date maturityDate, double fixedPayment, IborIndex iborIndex, Calendar paymentCalendar, BusinessDayConvention paymentConvention) : this(NQuantLibcPINVOKE.new_ZeroCouponSwap__SWIG_1((int)type, baseNominal, Date.getCPtr(startDate), Date.getCPtr(maturityDate), fixedPayment, IborIndex.getCPtr(iborIndex), Calendar.getCPtr(paymentCalendar), (int)paymentConvention), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZeroCouponSwap(Swap.Type type, double baseNominal, Date startDate, Date maturityDate, double fixedPayment, IborIndex iborIndex, Calendar paymentCalendar) : this(NQuantLibcPINVOKE.new_ZeroCouponSwap__SWIG_2((int)type, baseNominal, Date.getCPtr(startDate), Date.getCPtr(maturityDate), fixedPayment, IborIndex.getCPtr(iborIndex), Calendar.getCPtr(paymentCalendar)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZeroCouponSwap(Swap.Type type, double baseNominal, Date startDate, Date maturityDate, double fixedRate, DayCounter fixedDayCounter, IborIndex iborIndex, Calendar paymentCalendar, BusinessDayConvention paymentConvention, uint paymentDelay) : this(NQuantLibcPINVOKE.new_ZeroCouponSwap__SWIG_3((int)type, baseNominal, Date.getCPtr(startDate), Date.getCPtr(maturityDate), fixedRate, DayCounter.getCPtr(fixedDayCounter), IborIndex.getCPtr(iborIndex), Calendar.getCPtr(paymentCalendar), (int)paymentConvention, paymentDelay), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZeroCouponSwap(Swap.Type type, double baseNominal, Date startDate, Date maturityDate, double fixedRate, DayCounter fixedDayCounter, IborIndex iborIndex, Calendar paymentCalendar, BusinessDayConvention paymentConvention) : this(NQuantLibcPINVOKE.new_ZeroCouponSwap__SWIG_4((int)type, baseNominal, Date.getCPtr(startDate), Date.getCPtr(maturityDate), fixedRate, DayCounter.getCPtr(fixedDayCounter), IborIndex.getCPtr(iborIndex), Calendar.getCPtr(paymentCalendar), (int)paymentConvention), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZeroCouponSwap(Swap.Type type, double baseNominal, Date startDate, Date maturityDate, double fixedRate, DayCounter fixedDayCounter, IborIndex iborIndex, Calendar paymentCalendar) : this(NQuantLibcPINVOKE.new_ZeroCouponSwap__SWIG_5((int)type, baseNominal, Date.getCPtr(startDate), Date.getCPtr(maturityDate), fixedRate, DayCounter.getCPtr(fixedDayCounter), IborIndex.getCPtr(iborIndex), Calendar.getCPtr(paymentCalendar)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public Swap.Type type() {
    Swap.Type ret = (Swap.Type)NQuantLibcPINVOKE.ZeroCouponSwap_type(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double baseNominal() {
    double ret = NQuantLibcPINVOKE.ZeroCouponSwap_baseNominal(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public IborIndex iborIndex() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.ZeroCouponSwap_iborIndex(swigCPtr);
    IborIndex ret = (cPtr == global::System.IntPtr.Zero) ? null : new IborIndex(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Leg fixedLeg() {
    Leg ret = new Leg(NQuantLibcPINVOKE.ZeroCouponSwap_fixedLeg(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Leg floatingLeg() {
    Leg ret = new Leg(NQuantLibcPINVOKE.ZeroCouponSwap_floatingLeg(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fixedPayment() {
    double ret = NQuantLibcPINVOKE.ZeroCouponSwap_fixedPayment(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fixedLegNPV() {
    double ret = NQuantLibcPINVOKE.ZeroCouponSwap_fixedLegNPV(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double floatingLegNPV() {
    double ret = NQuantLibcPINVOKE.ZeroCouponSwap_floatingLegNPV(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fairFixedPayment() {
    double ret = NQuantLibcPINVOKE.ZeroCouponSwap_fairFixedPayment(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fairFixedRate(DayCounter dayCounter) {
    double ret = NQuantLibcPINVOKE.ZeroCouponSwap_fairFixedRate(swigCPtr, DayCounter.getCPtr(dayCounter));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
