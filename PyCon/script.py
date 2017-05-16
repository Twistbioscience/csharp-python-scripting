import time

time.sleep(1)
print("Fill DevA")
devA.Fill()
devAFillCount=1

time.sleep(2)
print("Fill DevB")
devB.Fill()
devBFillCount=1

time.sleep(2)
print("Empty DevA")
devA.Empty()
