//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class BlackCalibrationHelperVector : global::System.IDisposable, global::System.Collections.IEnumerable, global::System.Collections.Generic.IList<BlackCalibrationHelper>
 {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal BlackCalibrationHelperVector(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(BlackCalibrationHelperVector obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(BlackCalibrationHelperVector obj) {
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

  ~BlackCalibrationHelperVector() {
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
          NQuantLibcPINVOKE.delete_BlackCalibrationHelperVector(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public BlackCalibrationHelperVector(global::System.Collections.IEnumerable c) : this() {
    if (c == null)
      throw new global::System.ArgumentNullException("c");
    foreach (BlackCalibrationHelper element in c) {
      this.Add(element);
    }
  }

  public BlackCalibrationHelperVector(global::System.Collections.Generic.IEnumerable<BlackCalibrationHelper> c) : this() {
    if (c == null)
      throw new global::System.ArgumentNullException("c");
    foreach (BlackCalibrationHelper element in c) {
      this.Add(element);
    }
  }

  public bool IsFixedSize {
    get {
      return false;
    }
  }

  public bool IsReadOnly {
    get {
      return false;
    }
  }

  public BlackCalibrationHelper this[int index]  {
    get {
      return getitem(index);
    }
    set {
      setitem(index, value);
    }
  }

  public int Capacity {
    get {
      return (int)capacity();
    }
    set {
      if (value < 0 || (uint)value < size())
        throw new global::System.ArgumentOutOfRangeException("Capacity");
      reserve((uint)value);
    }
  }

  public bool IsEmpty {
    get {
      return empty();
    }
  }

  public int Count {
    get {
      return (int)size();
    }
  }

  public bool IsSynchronized {
    get {
      return false;
    }
  }

  public void CopyTo(BlackCalibrationHelper[] array)
  {
    CopyTo(0, array, 0, this.Count);
  }

  public void CopyTo(BlackCalibrationHelper[] array, int arrayIndex)
  {
    CopyTo(0, array, arrayIndex, this.Count);
  }

  public void CopyTo(int index, BlackCalibrationHelper[] array, int arrayIndex, int count)
  {
    if (array == null)
      throw new global::System.ArgumentNullException("array");
    if (index < 0)
      throw new global::System.ArgumentOutOfRangeException("index", "Value is less than zero");
    if (arrayIndex < 0)
      throw new global::System.ArgumentOutOfRangeException("arrayIndex", "Value is less than zero");
    if (count < 0)
      throw new global::System.ArgumentOutOfRangeException("count", "Value is less than zero");
    if (array.Rank > 1)
      throw new global::System.ArgumentException("Multi dimensional array.", "array");
    if (index+count > this.Count || arrayIndex+count > array.Length)
      throw new global::System.ArgumentException("Number of elements to copy is too large.");
    for (int i=0; i<count; i++)
      array.SetValue(getitemcopy(index+i), arrayIndex+i);
  }

  public BlackCalibrationHelper[] ToArray() {
    BlackCalibrationHelper[] array = new BlackCalibrationHelper[this.Count];
    this.CopyTo(array);
    return array;
  }

  global::System.Collections.Generic.IEnumerator<BlackCalibrationHelper> global::System.Collections.Generic.IEnumerable<BlackCalibrationHelper>.GetEnumerator() {
    return new BlackCalibrationHelperVectorEnumerator(this);
  }

  global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() {
    return new BlackCalibrationHelperVectorEnumerator(this);
  }

  public BlackCalibrationHelperVectorEnumerator GetEnumerator() {
    return new BlackCalibrationHelperVectorEnumerator(this);
  }

  // Type-safe enumerator
  /// Note that the IEnumerator documentation requires an InvalidOperationException to be thrown
  /// whenever the collection is modified. This has been done for changes in the size of the
  /// collection but not when one of the elements of the collection is modified as it is a bit
  /// tricky to detect unmanaged code that modifies the collection under our feet.
  public sealed class BlackCalibrationHelperVectorEnumerator : global::System.Collections.IEnumerator
    , global::System.Collections.Generic.IEnumerator<BlackCalibrationHelper>
  {
    private BlackCalibrationHelperVector collectionRef;
    private int currentIndex;
    private object currentObject;
    private int currentSize;

    public BlackCalibrationHelperVectorEnumerator(BlackCalibrationHelperVector collection) {
      collectionRef = collection;
      currentIndex = -1;
      currentObject = null;
      currentSize = collectionRef.Count;
    }

    // Type-safe iterator Current
    public BlackCalibrationHelper Current {
      get {
        if (currentIndex == -1)
          throw new global::System.InvalidOperationException("Enumeration not started.");
        if (currentIndex > currentSize - 1)
          throw new global::System.InvalidOperationException("Enumeration finished.");
        if (currentObject == null)
          throw new global::System.InvalidOperationException("Collection modified.");
        return (BlackCalibrationHelper)currentObject;
      }
    }

    // Type-unsafe IEnumerator.Current
    object global::System.Collections.IEnumerator.Current {
      get {
        return Current;
      }
    }

    public bool MoveNext() {
      int size = collectionRef.Count;
      bool moveOkay = (currentIndex+1 < size) && (size == currentSize);
      if (moveOkay) {
        currentIndex++;
        currentObject = collectionRef[currentIndex];
      } else {
        currentObject = null;
      }
      return moveOkay;
    }

    public void Reset() {
      currentIndex = -1;
      currentObject = null;
      if (collectionRef.Count != currentSize) {
        throw new global::System.InvalidOperationException("Collection modified.");
      }
    }

    public void Dispose() {
        currentIndex = -1;
        currentObject = null;
    }
  }

  public BlackCalibrationHelperVector() : this(NQuantLibcPINVOKE.new_BlackCalibrationHelperVector__SWIG_0(), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackCalibrationHelperVector(BlackCalibrationHelperVector other) : this(NQuantLibcPINVOKE.new_BlackCalibrationHelperVector__SWIG_1(BlackCalibrationHelperVector.getCPtr(other)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void Clear() {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_Clear(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void Add(BlackCalibrationHelper x) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_Add(swigCPtr, BlackCalibrationHelper.getCPtr(x));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  private uint size() {
    uint ret = NQuantLibcPINVOKE.BlackCalibrationHelperVector_size(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  private bool empty() {
    bool ret = NQuantLibcPINVOKE.BlackCalibrationHelperVector_empty(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  private uint capacity() {
    uint ret = NQuantLibcPINVOKE.BlackCalibrationHelperVector_capacity(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  private void reserve(uint n) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_reserve(swigCPtr, n);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackCalibrationHelperVector(int capacity) : this(NQuantLibcPINVOKE.new_BlackCalibrationHelperVector__SWIG_2(capacity), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  private BlackCalibrationHelper getitemcopy(int index) {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.BlackCalibrationHelperVector_getitemcopy(swigCPtr, index);
    BlackCalibrationHelper ret = (cPtr == global::System.IntPtr.Zero) ? null : new BlackCalibrationHelper(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  private BlackCalibrationHelper getitem(int index) {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.BlackCalibrationHelperVector_getitem(swigCPtr, index);
    BlackCalibrationHelper ret = (cPtr == global::System.IntPtr.Zero) ? null : new BlackCalibrationHelper(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  private void setitem(int index, BlackCalibrationHelper val) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_setitem(swigCPtr, index, BlackCalibrationHelper.getCPtr(val));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void AddRange(BlackCalibrationHelperVector values) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_AddRange(swigCPtr, BlackCalibrationHelperVector.getCPtr(values));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BlackCalibrationHelperVector GetRange(int index, int count) {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.BlackCalibrationHelperVector_GetRange(swigCPtr, index, count);
    BlackCalibrationHelperVector ret = (cPtr == global::System.IntPtr.Zero) ? null : new BlackCalibrationHelperVector(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void Insert(int index, BlackCalibrationHelper x) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_Insert(swigCPtr, index, BlackCalibrationHelper.getCPtr(x));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void InsertRange(int index, BlackCalibrationHelperVector values) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_InsertRange(swigCPtr, index, BlackCalibrationHelperVector.getCPtr(values));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void RemoveAt(int index) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_RemoveAt(swigCPtr, index);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void RemoveRange(int index, int count) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_RemoveRange(swigCPtr, index, count);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public static BlackCalibrationHelperVector Repeat(BlackCalibrationHelper value, int count) {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.BlackCalibrationHelperVector_Repeat(BlackCalibrationHelper.getCPtr(value), count);
    BlackCalibrationHelperVector ret = (cPtr == global::System.IntPtr.Zero) ? null : new BlackCalibrationHelperVector(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void Reverse() {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_Reverse__SWIG_0(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void Reverse(int index, int count) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_Reverse__SWIG_1(swigCPtr, index, count);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void SetRange(int index, BlackCalibrationHelperVector values) {
    NQuantLibcPINVOKE.BlackCalibrationHelperVector_SetRange(swigCPtr, index, BlackCalibrationHelperVector.getCPtr(values));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public bool Contains(BlackCalibrationHelper value) {
    bool ret = NQuantLibcPINVOKE.BlackCalibrationHelperVector_Contains(swigCPtr, BlackCalibrationHelper.getCPtr(value));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public int IndexOf(BlackCalibrationHelper value) {
    int ret = NQuantLibcPINVOKE.BlackCalibrationHelperVector_IndexOf(swigCPtr, BlackCalibrationHelper.getCPtr(value));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public int LastIndexOf(BlackCalibrationHelper value) {
    int ret = NQuantLibcPINVOKE.BlackCalibrationHelperVector_LastIndexOf(swigCPtr, BlackCalibrationHelper.getCPtr(value));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public bool Remove(BlackCalibrationHelper value) {
    bool ret = NQuantLibcPINVOKE.BlackCalibrationHelperVector_Remove(swigCPtr, BlackCalibrationHelper.getCPtr(value));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
