//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class AndreasenHugeVolatilityAdapter : BlackVolTermStructure {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal AndreasenHugeVolatilityAdapter(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.AndreasenHugeVolatilityAdapter_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(AndreasenHugeVolatilityAdapter obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_AndreasenHugeVolatilityAdapter(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public AndreasenHugeVolatilityAdapter(AndreasenHugeVolatilityInterpl volInterpl, double eps) : this(NQuantLibcPINVOKE.new_AndreasenHugeVolatilityAdapter__SWIG_0(AndreasenHugeVolatilityInterpl.getCPtr(volInterpl), eps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public AndreasenHugeVolatilityAdapter(AndreasenHugeVolatilityInterpl volInterpl) : this(NQuantLibcPINVOKE.new_AndreasenHugeVolatilityAdapter__SWIG_1(AndreasenHugeVolatilityInterpl.getCPtr(volInterpl)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
