const int LED_PIN = 13; // built-in led
const int TRIG_PIN = 3; // d3

const int PULSE =  100; // length of trig pulse in ms

void setup() {
  // initialize serial port
  Serial.begin(115200);
  while(!Serial){
    ;
  }


  // ensure that TRIG_PIN output starts LOW
  digitalWrite(TRIG_PIN, LOW);

  pinMode(LED_PIN, OUTPUT);
  pinMode(TRIG_PIN, INPUT);
}

void loop() {
  if( Serial.available() ){
    String cmd = Serial.readStringUntil('\n');
    if (cmd == "TRIG"){
      fireTrigger();
    }
  }
}



// sends a trigger pulse to TRIG_PIN for PULSE seconds
void fireTrigger(){
  digitalWrite(LED_PIN, HIGH); // for debug/sync
  pinMode(TRIG_PIN, OUTPUT);

  delay(PULSE);

  digitalWrite(LED_PIN, LOW);
  pinMode(TRIG_PIN, INPUT);
}