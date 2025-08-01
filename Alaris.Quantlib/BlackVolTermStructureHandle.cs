//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class BlackVolTermStructureHandle : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal BlackVolTermStructureHandle(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(BlackVolTermStructureHandle obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(BlackVolTermStructureHandle obj) {
    if (obj != null) {
      if (!obj.swigCMemOwn)
        throw new global::System.ApplicationException("Cannot release ownership as memory is not owned");
      global::System.Runtime.InteropServices.HandleRef ptr = obj.swigCPtr;
      obj.swigCMemOwn = false;
      obj.Dispose();
      return ptr;
    } else {
      return new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
    }
  }

  ~BlackVolTermStructureHandle() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    global::System.GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwn) {
          swigCMemOwn = false;
          NQuantLibcPINVOKE.delete_BlackVolTermStructureHandle(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public BlackVolTermStructureHandle() : this(NQuantLibcPINVOKE.new_BlackVolTermStructureHandle__SWIG_0(), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackVolTermStructureHandle(BlackVolTermStructure p, bool registerAsObserver) : this(NQuantLibcPINVOKE.new_BlackVolTermStructureHandle__SWIG_1(BlackVolTermStructure.getCPtr(p), registerAsObserver), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackVolTermStructureHandle(BlackVolTermStructure p) : this(NQuantLibcPINVOKE.new_BlackVolTermStructureHandle__SWIG_2(BlackVolTermStructure.getCPtr(p)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackVolTermStructure __deref__() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.BlackVolTermStructureHandle___deref__(swigCPtr);
    BlackVolTermStructure ret = (cPtr == global::System.IntPtr.Zero) ? null : new BlackVolTermStructure(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public BlackVolTermStructure currentLink() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.BlackVolTermStructureHandle_currentLink(swigCPtr);
    BlackVolTermStructure ret = (cPtr == global::System.IntPtr.Zero) ? null : new BlackVolTermStructure(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public bool empty() {
    bool ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_empty(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Observable asObservable() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.BlackVolTermStructureHandle_asObservable(swigCPtr);
    Observable ret = (cPtr == global::System.IntPtr.Zero) ? null : new Observable(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVol(Date arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVol__SWIG_0(swigCPtr, Date.getCPtr(arg0), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVol(Date arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVol__SWIG_1(swigCPtr, Date.getCPtr(arg0), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVol(double arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVol__SWIG_2(swigCPtr, arg0, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVol(double arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVol__SWIG_3(swigCPtr, arg0, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(Date arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVariance__SWIG_0(swigCPtr, Date.getCPtr(arg0), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(Date arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVariance__SWIG_1(swigCPtr, Date.getCPtr(arg0), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(double arg0, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVariance__SWIG_2(swigCPtr, arg0, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackVariance(double arg0, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackVariance__SWIG_3(swigCPtr, arg0, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(Date arg0, Date arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVol__SWIG_0(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(Date arg0, Date arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVol__SWIG_1(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(double arg0, double arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVol__SWIG_2(swigCPtr, arg0, arg1, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVol(double arg0, double arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVol__SWIG_3(swigCPtr, arg0, arg1, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(Date arg0, Date arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVariance__SWIG_0(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(Date arg0, Date arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVariance__SWIG_1(swigCPtr, Date.getCPtr(arg0), Date.getCPtr(arg1), strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(double arg0, double arg1, double strike, bool extrapolate) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVariance__SWIG_2(swigCPtr, arg0, arg1, strike, extrapolate);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double blackForwardVariance(double arg0, double arg1, double strike) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_blackForwardVariance__SWIG_3(swigCPtr, arg0, arg1, strike);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double minStrike() {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_minStrike(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double maxStrike() {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_maxStrike(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public DayCounter dayCounter() {
    DayCounter ret = new DayCounter(NQuantLibcPINVOKE.BlackVolTermStructureHandle_dayCounter(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double timeFromReference(Date date) {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_timeFromReference(swigCPtr, Date.getCPtr(date));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Calendar calendar() {
    Calendar ret = new Calendar(NQuantLibcPINVOKE.BlackVolTermStructureHandle_calendar(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Date referenceDate() {
    Date ret = new Date(NQuantLibcPINVOKE.BlackVolTermStructureHandle_referenceDate(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Date maxDate() {
    Date ret = new Date(NQuantLibcPINVOKE.BlackVolTermStructureHandle_maxDate(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double maxTime() {
    double ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_maxTime(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void enableExtrapolation() {
    NQuantLibcPINVOKE.BlackVolTermStructureHandle_enableExtrapolation(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void disableExtrapolation() {
    NQuantLibcPINVOKE.BlackVolTermStructureHandle_disableExtrapolation(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public bool allowsExtrapolation() {
    bool ret = NQuantLibcPINVOKE.BlackVolTermStructureHandle_allowsExtrapolation(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
